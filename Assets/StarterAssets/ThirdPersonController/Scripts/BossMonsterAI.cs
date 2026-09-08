using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public sealed class BossMonsterAI : MonoBehaviour
{
    public enum BossState
    {
        Chasing,
        Attacking,
        Dead
    }

    [Header("Current State")]
    [SerializeField] private BossState _currentState = BossState.Chasing;

    [Header("Combat Settings")]
    [SerializeField] private float _attackRange = 6.0f;
    [SerializeField] private int _attackDamage = 28;
    [SerializeField] private float _attackInterval = 2.4f;
    [SerializeField] private float _chaseSpeed = 4.2f;
    private float _nextAttackTime;

    [Header("References")]
    [SerializeField] private Animator _animator;
    [SerializeField] private NavMeshAgent _navMeshAgent;
    [SerializeField] private Health _health;
    [SerializeField] private Transform _playerTarget;

    private static readonly int StateIdle = Animator.StringToHash("Monster_anim|Idle_1");
    private static readonly int StateRun = Animator.StringToHash("Monster_anim|Run");
    private static readonly int StateAttack1 = Animator.StringToHash("Monster_anim|Atack");
    private static readonly int StateAttack2 = Animator.StringToHash("Monster_anim|Atack_2");
    private static readonly int StateDeath = Animator.StringToHash("Monster_anim|Death");

    public BossState CurrentState => _currentState;
    public Health BossHealth => _health;

    private void Awake()
    {
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_navMeshAgent == null) _navMeshAgent = GetComponent<NavMeshAgent>();
        if (_health == null) _health = GetComponent<Health>();
    }

    private void Start()
    {
        if (_health != null)
        {
            _health.OnDeath.AddListener(OnDeath);
        }

        if (_navMeshAgent != null)
        {
            _navMeshAgent.speed = _chaseSpeed;
            _navMeshAgent.stoppingDistance = _attackRange * 0.85f;
            _navMeshAgent.baseOffset = 0f;

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 10.0f, NavMesh.AllAreas))
            {
                _navMeshAgent.Warp(hit.position);
            }
        }

        FindPlayer();
        PlayAnimation(StateRun);
    }

    private void Update()
    {
        if (_currentState == BossState.Dead) return;

        if (_playerTarget == null)
        {
            FindPlayer();
            if (_playerTarget == null) return;
        }

        float distance = Vector3.Distance(transform.position, _playerTarget.position);

        switch (_currentState)
        {
            case BossState.Chasing:
                UpdateChasing(distance);
                break;
            case BossState.Attacking:
                UpdateAttacking(distance);
                break;
        }
    }

    private void UpdateChasing(float distance)
    {
        // Relentless pursuit: Never leashes or shakes off
        if (distance <= _attackRange)
        {
            _currentState = BossState.Attacking;
            if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
            {
                _navMeshAgent.isStopped = true;
            }
            return;
        }

        if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.isStopped = false;
            _navMeshAgent.speed = _chaseSpeed;
            _navMeshAgent.SetDestination(_playerTarget.position);
        }
    }

    private void UpdateAttacking(float distance)
    {
        if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.isStopped = true;
        }

        // Face player
        Vector3 dir = (_playerTarget.position - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 6.0f);
        }

        if (distance > _attackRange * 1.35f)
        {
            _currentState = BossState.Chasing;
            PlayAnimation(StateRun);
            return;
        }

        if (Time.time >= _nextAttackTime)
        {
            ExecuteBossAttack();
            _nextAttackTime = Time.time + _attackInterval;
        }
    }

    private void ExecuteBossAttack()
    {
        int anim = (Random.value > 0.5f) ? StateAttack1 : StateAttack2;
        PlayAnimation(anim);

        if (_playerTarget != null)
        {
            Health playerHealth = _playerTarget.GetComponent<Health>();
            if (playerHealth == null) playerHealth = _playerTarget.GetComponentInParent<Health>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(_attackDamage);
            }
        }
    }

    private void PlayAnimation(int stateHash)
    {
        if (_animator != null && _animator.HasState(0, stateHash))
        {
            _animator.CrossFade(stateHash, 0.15f, 0, 0f);
        }
    }

    private void FindPlayer()
    {
        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null) playerObj = GameObject.Find("PlayerArmature");
        if (playerObj != null)
        {
            _playerTarget = playerObj.transform;
        }
    }

    private void OnDeath()
    {
        if (_currentState == BossState.Dead) return;

        _currentState = BossState.Dead;
        PlayAnimation(StateDeath);

        if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled)
        {
            _navMeshAgent.isStopped = true;
            _navMeshAgent.enabled = false;
        }

        var colliders = GetComponentsInChildren<Collider>();
        foreach (var c in colliders) c.enabled = false;

        if (ApocalypseManager.Instance != null)
        {
            ApocalypseManager.Instance.OnBossDefeated();
        }

        Destroy(gameObject, 8.0f);
    }
}
