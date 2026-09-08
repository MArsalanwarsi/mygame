using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public sealed class OrcDestroyerBossAI : MonoBehaviour
{
    public enum BossState
    {
        Chasing,
        Attacking,
        Enraging,
        Dead
    }

    [System.Serializable]
    public struct AttackPattern
    {
        public string name;
        public string animationName;
        public int damage;
        public float windup;
        public float reach;
        public float cooldown;

        public AttackPattern(string name, string anim, int damage, float windup, float reach, float cooldown)
        {
            this.name = name;
            this.animationName = anim;
            this.damage = damage;
            this.windup = windup;
            this.reach = reach;
            this.cooldown = cooldown;
        }
    }

    [Header("Current State")]
    [SerializeField] private BossState _currentState = BossState.Chasing;
    [SerializeField] private bool _isEnraged = false;
    [SerializeField] private string _currentAttackName = "None";

    [Header("Stats")]
    [SerializeField] private int _maxHealth = 1500;
    [SerializeField] private float _baseSpeed = 4.8f;
    [SerializeField] private float _enragedSpeed = 6.0f;
    [SerializeField] private float _engagementRange = 7.5f;

    [Header("References")]
    [SerializeField] private Animator _animator;
    [SerializeField] private NavMeshAgent _navMeshAgent;
    [SerializeField] private Health _health;
    [SerializeField] private Transform _playerTarget;

    private AttackPattern[] _patterns;
    private int _lastPatternIndex = -1;
    private float _nextAttackAllowedTime;
    private Coroutine _attackRoutine;

    private static readonly int HashIdle = Animator.StringToHash("idle1");
    private static readonly int HashRun = Animator.StringToHash("run1");
    private static readonly int HashRage = Animator.StringToHash("rage2");
    private static readonly int HashDeath = Animator.StringToHash("death1");

    public BossState CurrentState => _currentState;
    public Health BossHealth => _health;
    public bool IsEnraged => _isEnraged;
    public string CurrentAttackName => _currentAttackName;

    private void Awake()
    {
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_navMeshAgent == null) _navMeshAgent = GetComponent<NavMeshAgent>();
        if (_health == null) _health = GetComponent<Health>();

        // Ensure 10x size
        transform.localScale = new Vector3(10f, 10f, 10f);

        // Configure CapsuleCollider for 10x scale
        if (TryGetComponent<CapsuleCollider>(out var col))
        {
            col.center = new Vector3(0f, 1.15f, 0f);
            col.radius = 0.85f;
            col.height = 2.4f;
            col.isTrigger = false;
        }

        // Configure NavMeshAgent with baseOffset = 0 to prevent floor sinking
        if (_navMeshAgent != null)
        {
            _navMeshAgent.baseOffset = 0f;
            _navMeshAgent.height = 14.0f;
            _navMeshAgent.radius = 2.6f;
            _navMeshAgent.stoppingDistance = 6.0f;
            _navMeshAgent.speed = _baseSpeed;
            _navMeshAgent.acceleration = 16f;
            _navMeshAgent.angularSpeed = 240f;
            _navMeshAgent.autoBraking = true;
        }

        InitAttackPatterns();
    }

    private void InitAttackPatterns()
    {
        _patterns = new AttackPattern[]
        {
            new AttackPattern("Heavy Cleave", "atack1", 45, 0.65f, 8.5f, 1.8f),
            new AttackPattern("Berserker Flurry", "atack2", 55, 0.55f, 8.0f, 1.7f),
            new AttackPattern("Seismic Ground Slam", "atack3", 75, 0.85f, 9.2f, 2.2f),
            new AttackPattern("Whirlwind Sweep", "atack5", 65, 0.70f, 8.8f, 2.0f),
            new AttackPattern("Titanic Brutal Smash", "atack7", 90, 0.95f, 9.8f, 2.5f)
        };
    }

    private void Start()
    {
        if (_health != null)
        {
            _health.SetMaxHealth(_maxHealth, true);
            _health.OnDeath.AddListener(OnDeath);
        }

        if (_navMeshAgent != null)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 20.0f, NavMesh.AllAreas))
            {
                _navMeshAgent.Warp(hit.position);
            }
        }

        FindPlayer();
        PlayAnimation(HashRun);
    }

    private void Update()
    {
        if (_currentState == BossState.Dead) return;

        if (_playerTarget == null)
        {
            FindPlayer();
            if (_playerTarget == null) return;
        }

        // Check for Enrage Phase at 50% HP
        CheckEnragePhase();

        float distance = Vector3.Distance(transform.position, _playerTarget.position);

        switch (_currentState)
        {
            case BossState.Chasing:
                UpdateChasing(distance);
                break;
            case BossState.Attacking:
                // Rotation towards player during attack windup is handled inside coroutine
                break;
            case BossState.Enraging:
                // Pauses movement while roaring
                break;
        }
    }

    private void CheckEnragePhase()
    {
        if (!_isEnraged && _health != null && _health.CurrentHealth > 0 && _health.CurrentHealth <= (_maxHealth / 2))
        {
            TriggerEnrage();
        }
    }

    private void TriggerEnrage()
    {
        _isEnraged = true;
        Debug.Log("[OrcDestroyerBoss] TITANIC ORC DESTROYER IS ENRAGED! Speed and fury heightened!");
        StartCoroutine(EnrageSequence());
    }

    private IEnumerator EnrageSequence()
    {
        _currentState = BossState.Enraging;
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }

        if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.isStopped = true;
        }

        PlayAnimation(HashRage);
        yield return new WaitForSeconds(2.0f);

        if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled)
        {
            _navMeshAgent.speed = _enragedSpeed;
            _navMeshAgent.isStopped = false;
        }

        _currentState = BossState.Chasing;
        PlayAnimation(HashRun);
    }

    private void UpdateChasing(float distance)
    {
        if (distance <= _engagementRange)
        {
            if (Time.time >= _nextAttackAllowedTime)
            {
                _currentState = BossState.Attacking;
                _attackRoutine = StartCoroutine(ExecuteAttackPattern());
            }
            else
            {
                // In range but cooling down: face player and idle
                FaceTarget();
                if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
                {
                    _navMeshAgent.isStopped = true;
                }
            }
            return;
        }

        // Relentless pursuit
        if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.isStopped = false;
            _navMeshAgent.speed = _isEnraged ? _enragedSpeed : _baseSpeed;
            _navMeshAgent.SetDestination(_playerTarget.position);
        }

        PlayAnimation(HashRun);
    }

    private IEnumerator ExecuteAttackPattern()
    {
        if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.isStopped = true;
        }

        // Pick next attack pattern, avoiding immediate duplicate
        int index = Random.Range(0, _patterns.Length);
        if (index == _lastPatternIndex && _patterns.Length > 1)
        {
            index = (index + 1) % _patterns.Length;
        }
        _lastPatternIndex = index;
        AttackPattern pattern = _patterns[index];
        _currentAttackName = pattern.name;

        int animHash = Animator.StringToHash(pattern.animationName);
        PlayAnimation(animHash);

        // Turn towards target during windup
        float elapsed = 0f;
        while (elapsed < pattern.windup)
        {
            FaceTarget();
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Damage delivery with reach check
        if (_playerTarget != null && _currentState != BossState.Dead)
        {
            float dist = Vector3.Distance(transform.position, _playerTarget.position);
            if (dist <= pattern.reach)
            {
                Health pHealth = _playerTarget.GetComponent<Health>();
                if (pHealth == null) pHealth = _playerTarget.GetComponentInParent<Health>();
                if (pHealth != null)
                {
                    pHealth.TakeDamage(pattern.damage);
                    Debug.Log($"[OrcDestroyerBoss] {pattern.name} HIT! Dealt {pattern.damage} damage to player.");
                }
            }
            else
            {
                Debug.Log($"[OrcDestroyerBoss] Player dodged {pattern.name}! (Distance {dist:F1}m > Reach {pattern.reach:F1}m)");
            }
        }

        // Wait for remaining attack duration
        float remainingTime = Mathf.Max(0.1f, pattern.cooldown - pattern.windup);
        yield return new WaitForSeconds(remainingTime);

        _currentAttackName = "None";
        _nextAttackAllowedTime = Time.time + (_isEnraged ? 0.8f : 1.4f);

        if (_currentState != BossState.Dead && _currentState != BossState.Enraging)
        {
            _currentState = BossState.Chasing;
            PlayAnimation(HashRun);
        }

        _attackRoutine = null;
    }

    private void FaceTarget()
    {
        if (_playerTarget == null) return;
        Vector3 dir = (_playerTarget.position - transform.position).normalized;
        dir.y = 0f;
        if (dir != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8.0f);
        }
    }

    private void PlayAnimation(int hash)
    {
        if (_animator != null && _animator.HasState(0, hash))
        {
            _animator.CrossFade(hash, 0.15f, 0, 0f);
        }
    }

    private void FindPlayer()
    {
        var p = GameObject.FindWithTag("Player");
        if (p == null) p = GameObject.Find("PlayerArmature");
        if (p != null)
        {
            _playerTarget = p.transform;
        }
    }

    private void OnDeath()
    {
        if (_currentState == BossState.Dead) return;

        _currentState = BossState.Dead;
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }

        PlayAnimation(HashDeath);

        if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled)
        {
            _navMeshAgent.isStopped = true;
            _navMeshAgent.enabled = false;
        }

        var cols = GetComponentsInChildren<Collider>();
        foreach (var c in cols)
        {
            c.enabled = false;
        }

        if (ApocalypseManager.Instance != null)
        {
            ApocalypseManager.Instance.OnOrcBossDefeated();
        }

        Debug.Log("[OrcDestroyerBoss] TITANIC ORC DESTROYER DEFEATED!");
        Destroy(gameObject, 8.0f);
    }
}
