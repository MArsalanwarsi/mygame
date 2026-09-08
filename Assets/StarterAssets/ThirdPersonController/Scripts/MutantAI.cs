using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public sealed class MutantAI : MonoBehaviour
{
    public enum AIState
    {
        Idle,
        Chase,
        Attack,
        Return,
        Dead
    }

    [Header("Current State (Debug)")]
    [SerializeField] private AIState _currentState = AIState.Idle;

    [Header("Detection & Leash Ranges")]
    [SerializeField] private float _detectionRadius = 15.0f;
    [SerializeField] private float _leashRadius = 25.0f;
    [SerializeField] private LayerMask _playerLayerMask = 1 << 8; // Layer 8: Player
    [SerializeField] private LayerMask _obstacleMask = (1 << 0) | (1 << 9); // Layer 0: Default, Layer 9: Environment
    [SerializeField] private Transform _playerTarget;

    [Header("Combat & Movement Settings")]
    [SerializeField] private float _chaseSpeed = 3.5f;
    [SerializeField] private float _returnSpeed = 2.0f;
    [SerializeField] private float _attackRange = 2.2f;
    [SerializeField] private float _attackCooldown = 2.0f;
    private float _nextAttackTime;

    [Header("Home Post / Spawning")]
    [SerializeField] private Vector3 _spawnPosition;
    [SerializeField] private Quaternion _spawnRotation;

    [Header("Dependencies")]
    [SerializeField] private Animator _animator;
    [SerializeField] private NavMeshAgent _navMeshAgent;
    [SerializeField] private Collider _weaponHitbox;
    [SerializeField] private Health _health;

    private readonly Collider[] _overlapBuffer = new Collider[4];
    private float _perceptionScanTimer;
    private const float PerceptionScanInterval = 0.2f;

    public AIState CurrentState => _currentState;

    private void Awake()
    {
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_navMeshAgent == null) _navMeshAgent = GetComponent<NavMeshAgent>();
        if (_health == null) _health = GetComponent<Health>();

        if (_weaponHitbox == null)
        {
            var cols = GetComponentsInChildren<Collider>();
            foreach (var c in cols)
            {
                if (c.gameObject != gameObject && c.isTrigger)
                {
                    _weaponHitbox = c;
                    break;
                }
            }
        }
    }

    private void Start()
    {
        DisableEnemyDamage();

        _spawnPosition = transform.position;
        _spawnRotation = transform.rotation;

        if (_health != null)
        {
            _health.OnDeath.AddListener(OnDeath);
        }

        if (_navMeshAgent != null)
        {
            _navMeshAgent.speed = _chaseSpeed;
            _navMeshAgent.stoppingDistance = _attackRange * 0.85f;

            // Ensure agent is snapped cleanly to NavMesh at start
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
            {
                _navMeshAgent.Warp(hit.position);
                _spawnPosition = hit.position;
            }
        }

        FindPlayerTarget();
        TransitionToState(AIState.Idle);
    }

    private void Update()
    {
        if (_currentState == AIState.Dead) return;

        _perceptionScanTimer -= Time.deltaTime;
        if (_perceptionScanTimer <= 0f)
        {
            _perceptionScanTimer = PerceptionScanInterval;
            PerformPerceptionScan();
        }

        switch (_currentState)
        {
            case AIState.Idle:
                UpdateIdle();
                break;
            case AIState.Chase:
                UpdateChase();
                break;
            case AIState.Attack:
                UpdateAttack();
                break;
            case AIState.Return:
                UpdateReturn();
                break;
        }
    }

    private void PerformPerceptionScan()
    {
        if (_playerTarget == null)
        {
            FindPlayerTarget();
        }

        if (_playerTarget == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, _playerTarget.position);

        if (_currentState == AIState.Idle || _currentState == AIState.Return)
        {
            if (distanceToPlayer <= _detectionRadius && HasLineOfSightToPlayer())
            {
                Debug.Log($"👁️ Ork spotted player at {distanceToPlayer:F1}m! Entering Chase state.");
                TransitionToState(AIState.Chase);
            }
        }
    }

    private bool HasLineOfSightToPlayer()
    {
        if (_playerTarget == null) return false;

        Vector3 eyePos = transform.position + Vector3.up * 1.6f;
        Vector3 targetPos = _playerTarget.position + Vector3.up * 1.0f;
        Vector3 dir = (targetPos - eyePos).normalized;
        float dist = Vector3.Distance(eyePos, targetPos);

        if (Physics.Raycast(eyePos, dir, out RaycastHit hit, dist, _obstacleMask))
        {
            if (hit.transform != _playerTarget && !hit.transform.IsChildOf(_playerTarget))
            {
                return false;
            }
        }
        return true;
    }

    private void UpdateIdle()
    {
        if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.isStopped = true;
        }
    }

    private void UpdateChase()
    {
        if (_playerTarget == null)
        {
            TransitionToState(AIState.Return);
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, _playerTarget.position);
        float distanceToSpawn = Vector3.Distance(transform.position, _spawnPosition);

        // LEASH CHECK: If player runs beyond leash distance, stop following and return
        if (distanceToPlayer > _leashRadius || distanceToSpawn > _leashRadius * 1.5f)
        {
            Debug.Log($"🛑 Player escaped beyond leash distance ({distanceToPlayer:F1}m). Ork breaking pursuit.");
            TransitionToState(AIState.Return);
            return;
        }

        // If in attack range, switch to Attack state
        if (distanceToPlayer <= _attackRange)
        {
            TransitionToState(AIState.Attack);
            return;
        }

        // Move via NavMeshAgent
        if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.isStopped = false;
            _navMeshAgent.speed = _chaseSpeed;
            _navMeshAgent.SetDestination(_playerTarget.position);
        }
    }

    private void UpdateAttack()
    {
        if (_playerTarget == null)
        {
            TransitionToState(AIState.Return);
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, _playerTarget.position);

        // If player fled beyond leash range while in combat
        if (distanceToPlayer > _leashRadius)
        {
            TransitionToState(AIState.Return);
            return;
        }

        // Stop agent from moving while swinging
        if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.isStopped = true;
        }

        // Smoothly face the player during attack state
        Vector3 lookDir = (_playerTarget.position - transform.position).normalized;
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
        }

        // Check if player has stepped outside attack range
        if (distanceToPlayer > _attackRange * 1.25f)
        {
            TransitionToState(AIState.Chase);
            return;
        }

        // Trigger attack on cooldown
        if (Time.time >= _nextAttackTime)
        {
            ExecuteEnemySwing();
            _nextAttackTime = Time.time + _attackCooldown;
        }
    }

    private void UpdateReturn()
    {
        float distanceToSpawn = Vector3.Distance(transform.position, _spawnPosition);

        // When close to spawn position, return to idle
        if (distanceToSpawn <= 1.0f)
        {
            transform.position = _spawnPosition;
            transform.rotation = Quaternion.Slerp(transform.rotation, _spawnRotation, Time.deltaTime * 5f);
            TransitionToState(AIState.Idle);
            return;
        }

        // Navigate back to spawn point
        if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.isStopped = false;
            _navMeshAgent.speed = _returnSpeed;
            _navMeshAgent.SetDestination(_spawnPosition);
        }
    }

    private void TransitionToState(AIState newState)
    {
        _currentState = newState;

        switch (newState)
        {
            case AIState.Idle:
                if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
                {
                    _navMeshAgent.isStopped = true;
                }
                PlayAnimationState("idle1");
                break;

            case AIState.Chase:
                if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
                {
                    _navMeshAgent.isStopped = false;
                }
                PlayAnimationState("run1");
                break;

            case AIState.Attack:
                if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
                {
                    _navMeshAgent.isStopped = true;
                }
                break;

            case AIState.Return:
                if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
                {
                    _navMeshAgent.isStopped = false;
                }
                PlayAnimationState("walk1");
                break;

            case AIState.Dead:
                if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled)
                {
                    _navMeshAgent.isStopped = true;
                    _navMeshAgent.enabled = false;
                }
                DisableEnemyDamage();
                PlayAnimationState("death1");
                break;
        }
    }

    private void ExecuteEnemySwing()
    {
        int attackIndex = Random.Range(1, 4); // 1, 2, or 3
        string attackState = "atack" + attackIndex;
        PlayAnimationState(attackState);
        Debug.Log($"👹 Ork AI executing attack: {attackState}");
    }

    private void PlayAnimationState(string stateName)
    {
        if (_animator != null && _animator.HasState(0, Animator.StringToHash(stateName)))
        {
            _animator.CrossFade(stateName, 0.15f, 0, 0f);
        }
    }

    private void FindPlayerTarget()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _detectionRadius, _overlapBuffer, _playerLayerMask);
        for (int i = 0; i < hitCount; i++)
        {
            if (_overlapBuffer[i].CompareTag("Player") || _overlapBuffer[i].name.Contains("Player"))
            {
                _playerTarget = _overlapBuffer[i].transform;
                return;
            }
        }

        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null) playerObj = GameObject.Find("PlayerArmature");
        if (playerObj != null)
        {
            _playerTarget = playerObj.transform;
        }
    }

    private void OnDeath()
    {
        TransitionToState(AIState.Dead);
        var colliders = GetComponentsInChildren<Collider>();
        foreach (var c in colliders) c.enabled = false;
    }

    // --- ANIMATION EVENT RECEIVERS ---
    public void BeginEnemyDamage()
    {
        if (_weaponHitbox != null && _currentState != AIState.Dead)
        {
            _weaponHitbox.enabled = true;
        }
    }

    public void EndEnemyDamage()
    {
        if (_weaponHitbox != null)
        {
            _weaponHitbox.enabled = false;
        }
    }

    public void DisableEnemyDamage()
    {
        if (_weaponHitbox != null)
        {
            _weaponHitbox.enabled = false;
        }
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
