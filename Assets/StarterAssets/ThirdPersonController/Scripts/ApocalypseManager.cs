using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public sealed class ApocalypseManager : MonoBehaviour
{
    public static ApocalypseManager Instance { get; private set; }

    [Header("Prefabs & Bosses")]
    [SerializeField] private GameObject _zombiePrefab;
    [SerializeField] private GameObject _bossPrefab;
    [SerializeField] private GameObject _orcBossPrefab;
    [SerializeField] private GameObject _orcBossSceneObject;

    [Header("Game Settings")]
    [SerializeField] private int _initialZombies = 6;
    [SerializeField] private int _killsNeededForBoss = 5;
    [SerializeField] private int _killsNeededForOrcBoss = 20;
    [SerializeField] private float _spawnMinRadius = 16.0f;
    [SerializeField] private float _spawnMaxRadius = 32.0f;

    [Header("Runtime State")]
    [SerializeField] private int _zombiesKilled;
    [SerializeField] private bool _bossSpawned;
    [SerializeField] private bool _bossDefeated;
    [SerializeField] private bool _orcBossSpawned;
    [SerializeField] private bool _orcBossDefeated;
    [SerializeField] private Transform _playerTransform;
    [SerializeField] private Health _playerHealth;

    private readonly List<ZombieAI> _activeZombies = new List<ZombieAI>();
    private BossMonsterAI _activeBossInstance;
    private OrcDestroyerBossAI _activeOrcBossInstance;
    private float _bannerDisplayTimer;
    private float _zombieSpawnIntervalTimer;
    private float _zombiesHpMultiplier = 1.0f;

    public int ZombiesKilled => _zombiesKilled;
    public int KillsNeededForBoss => _killsNeededForBoss;
    public int KillsNeededForOrcBoss => _killsNeededForOrcBoss;
    public bool BossSpawned => _bossSpawned;
    public bool BossDefeated => _bossDefeated;
    public bool OrcBossSpawned => _orcBossSpawned;
    public bool OrcBossDefeated => _orcBossDefeated;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        FindPlayer();
        FindOrcBossSceneObject();
        SpawnInitialHorde();
    }

    private void FindPlayer()
    {
        var p = GameObject.FindWithTag("Player");
        if (p == null) p = GameObject.Find("PlayerArmature");
        if (p != null)
        {
            _playerTransform = p.transform;
            _playerHealth = p.GetComponent<Health>();
        }
    }

    private void FindOrcBossSceneObject()
    {
        if (_orcBossSceneObject == null)
        {
            var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in allTransforms)
            {
                if (t.gameObject.name == "OrkDestroyer2")
                {
                    _orcBossSceneObject = t.gameObject;
                    _orcBossSceneObject.SetActive(false);
                    break;
                }
            }
        }
    }

    private void Update()
    {
        if (_bannerDisplayTimer > 0f)
        {
            _bannerDisplayTimer -= Time.deltaTime;
        }

        // Clean dead zombies from list
        _activeZombies.RemoveAll(z => z == null || z.CurrentState == ZombieAI.ZombieState.Dead);

        // Increased zombie spawn rate after Titan is defeated to maintain intense horde
        if (_bossDefeated && _activeZombies.Count < _initialZombies)
        {
            _zombieSpawnIntervalTimer += Time.deltaTime;
            if (_zombieSpawnIntervalTimer >= 2.5f)
            {
                _zombieSpawnIntervalTimer = 0f;
                Vector3 center = _playerTransform != null ? _playerTransform.position : Vector3.zero;
                SpawnZombieNear(center);
            }
        }
    }

    private void SpawnInitialHorde()
    {
        if (_zombiePrefab == null) return;
        if (_playerTransform == null) FindPlayer();

        Vector3 center = _playerTransform != null ? _playerTransform.position : Vector3.zero;

        for (int i = 0; i < _initialZombies; i++)
        {
            SpawnZombieNear(center);
        }
    }

    private void SpawnZombieNear(Vector3 center)
    {
        if (_zombiePrefab == null) return;

        Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(_spawnMinRadius, _spawnMaxRadius);
        Vector3 candidatePos = center + new Vector3(randomCircle.x, 0f, randomCircle.y);

        if (NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, 15.0f, NavMesh.AllAreas))
        {
            GameObject zObj = Instantiate(_zombiePrefab, hit.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            ZombieAI zAI = zObj.GetComponent<ZombieAI>();
            if (zAI != null)
            {
                _activeZombies.Add(zAI);
            }

            // Apply +20% HP after Titan boss defeat (50 -> 60 HP)
            if (_bossDefeated)
            {
                Health zHealth = zObj.GetComponent<Health>();
                if (zHealth != null)
                {
                    int boostedHp = Mathf.RoundToInt(50 * _zombiesHpMultiplier);
                    zHealth.SetMaxHealth(boostedHp, true);
                }
            }
        }
    }

    public void OnZombieKilled(ZombieAI zombie)
    {
        _zombiesKilled++;
        Debug.Log($"[ApocalypseManager] Zombie defeated! Total kills: {_zombiesKilled} (Titan at: {_killsNeededForBoss}, Orc Destroyer 2 at: {_killsNeededForOrcBoss})");

        // Recover 10 HP for the player upon defeating each zombie
        if (_playerHealth == null) FindPlayer();
        if (_playerHealth != null && _playerHealth.CurrentHealth > 0)
        {
            _playerHealth.Heal(10);
        }

        // Spawn sequence
        if (!_bossSpawned && _zombiesKilled >= _killsNeededForBoss)
        {
            SpawnBoss();
        }
        else if (_bossDefeated && !_orcBossSpawned && _zombiesKilled >= _killsNeededForOrcBoss)
        {
            SpawnOrcBoss();
        }
        else if (!_bossSpawned && _activeZombies.Count < _initialZombies)
        {
            Vector3 center = _playerTransform != null ? _playerTransform.position : Vector3.zero;
            SpawnZombieNear(center);
        }
        else if (_bossDefeated && _activeZombies.Count < _initialZombies)
        {
            Vector3 center = _playerTransform != null ? _playerTransform.position : Vector3.zero;
            SpawnZombieNear(center);
            // Spawn second zombie to increase spawn density rapidly
            SpawnZombieNear(center);
        }
    }

    private void SpawnBoss()
    {
        if (_bossSpawned || _bossPrefab == null) return;

        _bossSpawned = true;
        _bannerDisplayTimer = 6.0f;

        Vector3 playerPos = _playerTransform != null ? _playerTransform.position : Vector3.zero;
        Vector2 randomDir = Random.insideUnitCircle.normalized * 25.0f;
        Vector3 spawnTarget = playerPos + new Vector3(randomDir.x, 0f, randomDir.y);

        if (NavMesh.SamplePosition(spawnTarget, out NavMeshHit hit, 20.0f, NavMesh.AllAreas))
        {
            GameObject bossObj = Instantiate(_bossPrefab, hit.position, Quaternion.identity);
            _activeBossInstance = bossObj.GetComponent<BossMonsterAI>();
            Debug.Log($"[ApocalypseManager] 👹 10X TITAN BOSS SPAWNED at {hit.position}!");
        }
    }

    public void OnBossDefeated()
    {
        _bossDefeated = true;
        _bannerDisplayTimer = 8.0f;
        Debug.Log("[ApocalypseManager] 🎉 VICTORY! TITAN BOSS ELIMINATED!");

        // 1. Player health becomes completely full
        if (_playerHealth == null) FindPlayer();
        if (_playerHealth != null)
        {
            _playerHealth.Heal(_playerHealth.MaxHealth);
            Debug.Log("💚 [ApocalypseManager] Player HP fully restored after Titan defeat!");
        }

        // 2. Zombie spawn density increased
        _initialZombies = 12;

        // 3. Zombie max HP increased by 20% (50 -> 60 HP)
        _zombiesHpMultiplier = 1.2f;

        // Boost any active zombies to the new 60 HP
        foreach (var z in _activeZombies)
        {
            if (z != null && z.TryGetComponent<Health>(out var zh))
            {
                zh.SetMaxHealth(Mathf.RoundToInt(50 * _zombiesHpMultiplier), true);
            }
        }

        // Immediately spawn new zombies to reach higher density
        Vector3 center = _playerTransform != null ? _playerTransform.position : Vector3.zero;
        while (_activeZombies.Count < _initialZombies)
        {
            SpawnZombieNear(center);
        }
    }

    private void SpawnOrcBoss()
    {
        if (_orcBossSpawned) return;

        _orcBossSpawned = true;
        _bannerDisplayTimer = 8.0f;

        Vector3 playerPos = _playerTransform != null ? _playerTransform.position : Vector3.zero;
        Vector2 randomDir = Random.insideUnitCircle.normalized * 25.0f;
        Vector3 spawnTarget = playerPos + new Vector3(randomDir.x, 0f, randomDir.y);

        Vector3 spawnPos = spawnTarget;
        if (NavMesh.SamplePosition(spawnTarget, out NavMeshHit hit, 25.0f, NavMesh.AllAreas))
        {
            spawnPos = hit.position;
        }

        GameObject orcObj = null;
        if (_orcBossSceneObject != null)
        {
            orcObj = _orcBossSceneObject;
            orcObj.transform.position = spawnPos;
            orcObj.SetActive(true);
        }
        else if (_orcBossPrefab != null)
        {
            orcObj = Instantiate(_orcBossPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in allTransforms)
            {
                if (t.gameObject.name.Contains("OrkDestroyer2"))
                {
                    orcObj = t.gameObject;
                    orcObj.transform.position = spawnPos;
                    orcObj.SetActive(true);
                    break;
                }
            }
        }

        if (orcObj != null)
        {
            orcObj.transform.localScale = Vector3.one * 10.0f;

            // Ensure OrcDestroyerBossAI component
            _activeOrcBossInstance = orcObj.GetComponent<OrcDestroyerBossAI>();
            if (_activeOrcBossInstance == null)
            {
                _activeOrcBossInstance = orcObj.AddComponent<OrcDestroyerBossAI>();
            }

            // Remove legacy MutantAI if present
            var mutantAI = orcObj.GetComponent<MutantAI>();
            if (mutantAI != null)
            {
                Destroy(mutantAI);
            }

            // Ensure NavMeshAgent is on surface with baseOffset = 0
            NavMeshAgent agent = orcObj.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.baseOffset = 0f;
                agent.enabled = true;
                agent.Warp(spawnPos);
            }

            Debug.Log($"[ApocalypseManager] 👹👹 10X TITANIC ORC DESTROYER 2 HAS RISEN at {spawnPos}!");
        }
        else
        {
            Debug.LogError("[ApocalypseManager] Could not find or instantiate OrkDestroyer2!");
        }
    }

    public void OnOrcBossDefeated()
    {
        _orcBossDefeated = true;
        _bannerDisplayTimer = 15.0f;
        Debug.Log("[ApocalypseManager] 🏆 GRAND VICTORY! TITANIC ORC DESTROYER 2 HAS BEEN SLAIN!");

        // Reward player with full health again
        if (_playerHealth == null) FindPlayer();
        if (_playerHealth != null)
        {
            _playerHealth.Heal(_playerHealth.MaxHealth);
        }
    }

    private void OnGUI()
    {
        if (GameMenuManager.Instance != null && GameMenuManager.Instance.CurrentState != GameMenuManager.MenuState.Playing) return;

        int sw = Screen.width;

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        Rect hudRect = new Rect(sw / 2 - 220, 15, 440, 45);

        // State 1: Hunting towards Titan Boss (kills < 5)
        if (!_bossSpawned)
        {
            GUI.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
            GUI.contentColor = Color.white;
            GUI.Box(hudRect, $"🧟 ZOMBIE APOCALYPSE\nKills: {_zombiesKilled} / {_killsNeededForBoss} (Next: Titan)", boxStyle);
        }
        // State 2: Fighting Titan Boss
        else if (!_bossDefeated)
        {
            GUI.backgroundColor = new Color(0.6f, 0.05f, 0.05f, 0.9f);
            GUI.contentColor = Color.yellow;
            GUI.Box(hudRect, "⚠️ TITAN TROLL ACTIVE - DESTROY IT! ⚠️", boxStyle);

            if (_activeBossInstance != null && _activeBossInstance.BossHealth != null)
            {
                RenderBossBar(sw, _activeBossInstance.BossHealth.CurrentHealth, _activeBossInstance.BossHealth.MaxHealth, "👹 TITAN HEALTH", Color.red);
            }
        }
        // State 3: Titan dead, hunting towards Orc Destroyer 2 (kills < 20)
        else if (!_orcBossSpawned)
        {
            GUI.backgroundColor = new Color(0.15f, 0.25f, 0.45f, 0.9f);
            GUI.contentColor = Color.cyan;
            GUI.Box(hudRect, $"🔥 HORDE INTENSIFIED (+20% HP)\nKills: {_zombiesKilled} / {_killsNeededForOrcBoss} (Next: Orc Destroyer 2)", boxStyle);
        }
        // State 4: Fighting Orc Destroyer 2
        else if (!_orcBossDefeated)
        {
            bool enraged = _activeOrcBossInstance != null && _activeOrcBossInstance.IsEnraged;
            GUI.backgroundColor = enraged ? new Color(0.85f, 0.1f, 0f, 0.95f) : new Color(0.55f, 0.05f, 0.05f, 0.9f);
            GUI.contentColor = enraged ? Color.red : Color.yellow;

            string attackInfo = (_activeOrcBossInstance != null && _activeOrcBossInstance.CurrentAttackName != "None") 
                ? $" [{_activeOrcBossInstance.CurrentAttackName}]" 
                : "";
            string title = enraged ? $"🔥👹 ENRAGED ORC DESTROYER 2{attackInfo} 👹🔥" : $"👹 ORC DESTROYER 2 ACTIVE{attackInfo} 👹";
            GUI.Box(hudRect, title, boxStyle);

            if (_activeOrcBossInstance != null && _activeOrcBossInstance.BossHealth != null)
            {
                Color barCol = enraged ? new Color(1f, 0.2f, 0f) : new Color(0.85f, 0.15f, 0.15f);
                RenderBossBar(sw, _activeOrcBossInstance.BossHealth.CurrentHealth, _activeOrcBossInstance.BossHealth.MaxHealth, "👹 ORC DESTROYER 2", barCol);
            }
        }
        // State 5: All Bosses Slain
        else
        {
            GUI.backgroundColor = new Color(0.05f, 0.5f, 0.1f, 0.9f);
            GUI.contentColor = Color.white;
            GUI.Box(hudRect, "🏆 APOCALYPSE CONQUERED! ALL TITANS DESTROYED! 🏆", boxStyle);
        }

        // Warning / Victory Banners
        if (_bannerDisplayTimer > 0f)
        {
            GUIStyle bannerStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            if (!_bossDefeated)
            {
                GUI.backgroundColor = new Color(0.8f, 0f, 0f, 0.95f);
                GUI.contentColor = Color.yellow;
                GUI.Box(new Rect(sw / 2 - 340, 115, 680, 65), "⚠️ A 10X TITANIC MONSTER HAS RISEN! ⚠️\nDEFEAT IT OR DIE!", bannerStyle);
            }
            else if (!_orcBossSpawned)
            {
                GUI.backgroundColor = new Color(0f, 0.6f, 0.15f, 0.95f);
                GUI.contentColor = Color.white;
                GUI.Box(new Rect(sw / 2 - 340, 115, 680, 65), "🏆 TITAN DEFEATED! FULL HEALTH RESTORED! 🏆\nWARNING: HORDE HAS EXPANDED & MUTATED (+20% HP)!", bannerStyle);
            }
            else if (!_orcBossDefeated)
            {
                GUI.backgroundColor = new Color(0.85f, 0.15f, 0f, 0.95f);
                GUI.contentColor = Color.yellow;
                GUI.Box(new Rect(sw / 2 - 340, 115, 680, 65), "👹⚠️ 10X TITANIC ORC DESTROYER 2 HAS ARRIVED! ⚠️👹\nDEVASTATING ATTACKS! FIGHT FOR YOUR LIFE!", bannerStyle);
            }
            else
            {
                GUI.backgroundColor = new Color(0f, 0.7f, 0.2f, 0.95f);
                GUI.contentColor = Color.white;
                GUI.Box(new Rect(sw / 2 - 340, 115, 680, 65), "👑 ULTIMATE SURVIVOR! 👑\nYOU HAVE DEFEATED ALL TITANS OF THE APOCALYPSE!", bannerStyle);
            }
        }
    }

    private void RenderBossBar(int sw, int currentHp, int maxHp, string label, Color fillCol)
    {
        Rect barBg = new Rect(sw / 2 - 175, 65, 350, 22);
        GUI.backgroundColor = Color.black;
        GUI.Box(barBg, "");

        float ratio = maxHp > 0 ? Mathf.Clamp01((float)currentHp / maxHp) : 0f;
        GUI.backgroundColor = fillCol;
        GUI.Box(new Rect(sw / 2 - 175, 65, 350 * ratio, 22), "");

        GUIStyle bossLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        bossLabelStyle.normal.textColor = Color.white;
        GUI.Label(barBg, $"{label}: {currentHp} / {maxHp}", bossLabelStyle);
    }
}
