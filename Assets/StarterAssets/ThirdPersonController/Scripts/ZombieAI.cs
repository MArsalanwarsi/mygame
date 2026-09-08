using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public sealed class ZombieAI : MonoBehaviour
{
    public enum ZombieState
    {
        Roam,
        Chase,
        Attack,
        Dead
    }

    [Header("Current State")]
    [SerializeField] private ZombieState _currentState = ZombieState.Roam;

    [Header("Detection & Leash Ranges")]
    [Tooltip("Distance at which the zombie detects the player and starts running")]
    [SerializeField] private float _detectionRadius = 14.0f;

    [Tooltip("Distance beyond which the zombie breaks pursuit and resumes roaming")]
    [SerializeField] private float _leashRadius = 24.0f;

    [Tooltip("Attack range in meters")]
    [SerializeField] private float _attackRange = 1.35f;

    [Header("Movement Speeds")]
    [SerializeField] private float _walkSpeed = 1.8f;
    [SerializeField] private float _runSpeed = 4.2f;
    [SerializeField] private float _roamRadius = 18.0f;

    [Header("Combat Settings")]
    [SerializeField] private int _attackDamage = 7;
    [SerializeField] private float _attackInterval = 1.5f;
    private float _nextAttackTime;

    [Header("References")]
    [SerializeField] private Animator _animator;
    [SerializeField] private NavMeshAgent _navMeshAgent;
    [SerializeField] private Health _health;
    [SerializeField] private Transform _playerTarget;

    private float _roamTimer;
    private float _perceptionTimer;
    private const float PerceptionInterval = 0.2f;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int DieHash = Animator.StringToHash("Die");

    public ZombieState CurrentState => _currentState;

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
            _navMeshAgent.baseOffset = 0f;
            _navMeshAgent.stoppingDistance = Mathf.Max(0.9f, _attackRange * 0.8f);

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
            {
                _navMeshAgent.Warp(hit.position);
            }
        }

        FindPlayer();
        TransitionToState(ZombieState.Roam);
    }

    private void Update()
    {
        if (_currentState == ZombieState.Dead) return;

        _perceptionTimer -= Time.deltaTime;
        if (_perceptionTimer <= 0f)
        {
            _perceptionTimer = PerceptionInterval;
            PerformPerceptionScan();
        }

        switch (_currentState)
        {
            case ZombieState.Roam:
                UpdateRoam();
                break;
            case ZombieState.Chase:
                UpdateChase();
                break;
            case ZombieState.Attack:
                UpdateAttack();
                break;
        }
    }

    private void PerformPerceptionScan()
    {
        if (_playerTarget == null) FindPlayer();
        if (_playerTarget == null) return;

        float distance = Vector3.Distance(transform.position, _playerTarget.position);

        if (_currentState == ZombieState.Roam)
        {
            if (distance <= _detectionRadius && HasLineOfSightToPlayer())
            {
                TransitionToState(ZombieState.Chase);
            }
        }
    }

    private bool HasLineOfSightToPlayer()
    {
        if (_playerTarget == null) return false;

        Vector3 eyePos = transform.position + Vector3.up * 1.5f;
        Vector3 targetPos = _playerTarget.position + Vector3.up * 1.0f;
        Vector3 direction = (targetPos - eyePos).normalized;
        float distance = Vector3.Distance(eyePos, targetPos);

        int mask = (1 << 0) | (1 << 9);
        if (Physics.Raycast(eyePos, direction, out RaycastHit hit, distance, mask))
        {
            if (hit.transform != _playerTarget && !hit.transform.IsChildOf(_playerTarget))
            {
                return false;
            }
        }
        return true;
    }

    private void UpdateRoam()
    {
        _roamTimer -= Time.deltaTime;

        if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
        {
            if (_roamTimer <= 0f || _navMeshAgent.remainingDistance <= _navMeshAgent.stoppingDistance + 0.3f)
            {
                PickRandomRoamDestination();
                _roamTimer = Random.Range(5.0f, 10.0f);
            }
        }
    }

    private void PickRandomRoamDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * _roamRadius;
        randomDirection += transform.position;

        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, _roamRadius, NavMesh.AllAreas))
        {
            if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
            {
                _navMeshAgent.isStopped = false;
                _navMeshAgent.speed = _walkSpeed;
                _navMeshAgent.SetDestination(hit.position);
            }
        }
    }

    private void UpdateChase()
    {
        if (_playerTarget == null)
        {
            TransitionToState(ZombieState.Roam);
            return;
        }

        float distance = Vector3.Distance(transform.position, _playerTarget.position);

        if (distance > _leashRadius)
        {
            TransitionToState(ZombieState.Roam);
            return;
        }

        if (distance <= _attackRange)
        {
            TransitionToState(ZombieState.Attack);
            return;
        }

        if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.isStopped = false;
            _navMeshAgent.speed = _runSpeed;
            _navMeshAgent.SetDestination(_playerTarget.position);
        }
    }

    private void UpdateAttack()
    {
        if (_playerTarget == null)
        {
            TransitionToState(ZombieState.Roam);
            return;
        }

        float distance = Vector3.Distance(transform.position, _playerTarget.position);

        if (distance > _leashRadius)
        {
            TransitionToState(ZombieState.Roam);
            return;
        }

        if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.isStopped = true;
        }

        Vector3 lookDir = (_playerTarget.position - transform.position).normalized;
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 8.0f);
        }

        if (distance > _attackRange * 1.3f)
        {
            TransitionToState(ZombieState.Chase);
            return;
        }

        if (Time.time >= _nextAttackTime)
        {
            ExecuteAttack();
            _nextAttackTime = Time.time + _attackInterval;
        }
    }

    private void ExecuteAttack()
    {
        if (_animator != null)
        {
            _animator.SetTrigger(AttackHash);
        }

        StartCoroutine(DealAttackDamageDelayed(0.35f));
    }

    private IEnumerator DealAttackDamageDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (_currentState == ZombieState.Dead) yield break;

        if (_playerTarget == null) FindPlayer();

        if (_playerTarget != null)
        {
            float distance = Vector3.Distance(transform.position, _playerTarget.position);
            // Player can dodge or back away during swing windup: only apply damage if still in reach
            if (distance <= _attackRange * 1.25f)
            {
                Health playerHealth = _playerTarget.GetComponent<Health>();
                if (playerHealth == null) playerHealth = _playerTarget.GetComponentInParent<Health>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(_attackDamage);
                }
            }
        }
    }

    private void TransitionToState(ZombieState newState)
    {
        _currentState = newState;

        switch (newState)
        {
            case ZombieState.Roam:
                if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
                {
                    _navMeshAgent.isStopped = false;
                    _navMeshAgent.speed = _walkSpeed;
                }
                SetAnimatorSpeed(_walkSpeed);
                break;

            case ZombieState.Chase:
                if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
                {
                    _navMeshAgent.isStopped = false;
                    _navMeshAgent.speed = _runSpeed;
                }
                SetAnimatorSpeed(_runSpeed);
                break;

            case ZombieState.Attack:
                if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
                {
                    _navMeshAgent.isStopped = true;
                }
                SetAnimatorSpeed(0f);
                break;

            case ZombieState.Dead:
                if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled)
                {
                    _navMeshAgent.isStopped = true;
                    _navMeshAgent.enabled = false;
                }
                SetAnimatorSpeed(0f);
                if (_animator != null)
                {
                    _animator.SetTrigger(DieHash);
                }
                break;
        }
    }

    private void SetAnimatorSpeed(float speed)
    {
        if (_animator != null)
        {
            _animator.SetFloat(SpeedHash, speed);
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
        if (_currentState == ZombieState.Dead) return;

        TransitionToState(ZombieState.Dead);

        var colliders = GetComponentsInChildren<Collider>();
        foreach (var c in colliders) c.enabled = false;

        if (ApocalypseManager.Instance != null)
        {
            ApocalypseManager.Instance.OnZombieKilled(this);
        }
        else
        {
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj == null) playerObj = GameObject.Find("PlayerArmature");
            if (playerObj != null && playerObj.TryGetComponent<Health>(out var playerHp))
            {
                playerHp.Heal(10);
            }
        }

        Destroy(gameObject, 2.5f);
    }

    // --- ANIMATION EVENT RECEIVERS ---
    private void OnFootstep(AnimationEvent animationEvent)
    {
        // Suppresses the missing receiver warning and handles zombie footstep timing cleanly
    }

    private void OnLand(AnimationEvent animationEvent)
    {
        // Suppresses the missing receiver warning
    }

    public void BeginAttackHit()
    {
        // Handles attack animation event
    }

    public void EndAttackHit()
    {
        // Handles attack animation event
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _leashRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _attackRange);
    }
}
