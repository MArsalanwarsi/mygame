using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameMenuManager : MonoBehaviour
{
    public static GameMenuManager Instance { get; private set; }

    public enum MenuState
    {
        StartScreen,
        Playing,
        Paused,
        OptionsMenu,
        DeathScreen
    }

    [Header("Current State")]
    [SerializeField] private MenuState _currentState = MenuState.StartScreen;
    private MenuState _previousState = MenuState.StartScreen;

    [Header("CitrioN SMC Integration")]
    [Tooltip("The instantiated SettingsMenu_UGUI prefab from CitrioN SMC")]
    [SerializeField] private GameObject _citrionOptionsMenu;

    [Header("Title Screen Background")]
    [SerializeField] private Texture2D _horrorCityBg;

    [Header("Player Reference")]
    [SerializeField] private GameObject _playerObject;
    [SerializeField] private CharacterController _playerController;

    public MenuState CurrentState => _currentState;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        EnsureEventSystem();
        LoadHorrorCityBg();
        FindCitrioNMenu();
    }

    private void Start()
    {
        FindPlayer();
        // Start directly in the Start Screen
        SetState(MenuState.StartScreen);
    }

    private void EnsureEventSystem()
    {
        var existing = FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include);
        if (existing != null && existing.Length > 0)
        {
            // Ensure only 1 EventSystem exists
            for (int i = 1; i < existing.Length; i++)
            {
                Destroy(existing[i].gameObject);
            }
            return;
        }

        var esObj = new GameObject("EventSystem");
        esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    private void LoadHorrorCityBg()
    {
        if (_horrorCityBg == null)
        {
            string path = System.IO.Path.Combine(Application.dataPath, "horror_city_bg.jpg");
            if (System.IO.File.Exists(path))
            {
                byte[] bytes = System.IO.File.ReadAllBytes(path);
                _horrorCityBg = new Texture2D(2, 2);
                _horrorCityBg.LoadImage(bytes);
            }
        }
    }

    private void FindCitrioNMenu()
    {
        if (_citrionOptionsMenu == null)
        {
            _citrionOptionsMenu = GameObject.Find("SettingsMenu_UGUI");
        }

        if (_citrionOptionsMenu != null)
        {
            // Keep the GameObject active so its internal scripts initialize, but hide the Canvas
            _citrionOptionsMenu.SetActive(true);
            var canvas = _citrionOptionsMenu.GetComponent<Canvas>();
            if (canvas != null) canvas.enabled = false;
        }
    }

    private void SetCitrioNMenuVisible(bool visible)
    {
        if (_citrionOptionsMenu == null) FindCitrioNMenu();

        if (_citrionOptionsMenu != null)
        {
            _citrionOptionsMenu.SetActive(true);

            var panel = _citrionOptionsMenu.GetComponent<CitrioN.UI.CanvasUIPanel>();
            if (panel != null)
            {
                if (visible) panel.OpenNoParams();
                else panel.CloseNoParams();
            }

            var canvas = _citrionOptionsMenu.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.enabled = visible;
            }
        }
    }

    private void FindPlayer()
    {
        if (_playerObject == null)
        {
            _playerObject = GameObject.FindWithTag("Player");
            if (_playerObject == null) _playerObject = GameObject.Find("PlayerArmature");
        }

        if (_playerObject != null && _playerController == null)
        {
            _playerController = _playerObject.GetComponent<CharacterController>();
        }
    }

    private void Update()
    {
        // ESC key handler using Input System
        bool escapePressed = false;
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            escapePressed = true;
        }
#else
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            escapePressed = true;
        }
#endif

        if (escapePressed)
        {
            if (_currentState == MenuState.Playing)
            {
                PauseGame();
            }
            else if (_currentState == MenuState.Paused)
            {
                ResumeGame();
            }
            else if (_currentState == MenuState.OptionsMenu)
            {
                CloseOptionsMenu();
            }
        }
    }

    public void StartGame()
    {
        SetState(MenuState.Playing);
    }

    public void PauseGame()
    {
        SetState(MenuState.Paused);
    }

    public void ResumeGame()
    {
        SetState(MenuState.Playing);
    }

    public void OpenOptionsMenu(MenuState caller)
    {
        _previousState = caller;
        SetState(MenuState.OptionsMenu);
    }

    public void CloseOptionsMenu()
    {
        SetState(_previousState);
    }

    public void RestartGame()
    {
        Time.timeScale = 1.0f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1.0f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
        Debug.Log("[GameMenuManager] Quitting application...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OnPlayerDied()
    {
        StartCoroutine(PlayerDeathRoutine());
    }

    private IEnumerator PlayerDeathRoutine()
    {
        // Allow full-body death animation to play out in real-time (3.5 seconds)
        yield return new WaitForSeconds(3.5f);
        SetState(MenuState.DeathScreen);
    }

    private void SetState(MenuState newState)
    {
        _currentState = newState;

        // Manage CitrioN Options Menu visibility
        SetCitrioNMenuVisible(_currentState == MenuState.OptionsMenu);

        switch (_currentState)
        {
            case MenuState.StartScreen:
                Time.timeScale = 0.0f;
                SetCursor(true);
                SetPlayerControls(false);
                break;

            case MenuState.Playing:
                Time.timeScale = 1.0f;
                SetCursor(false);
                SetPlayerControls(true);
                break;

            case MenuState.Paused:
                Time.timeScale = 0.0f;
                SetCursor(true);
                SetPlayerControls(false);
                break;

            case MenuState.OptionsMenu:
                Time.timeScale = 0.0f;
                SetCursor(true);
                SetPlayerControls(false);
                break;

            case MenuState.DeathScreen:
                Time.timeScale = 0.0f;
                SetCursor(true);
                SetPlayerControls(false);
                break;
        }
    }

    private void SetCursor(bool visible)
    {
        Cursor.visible = visible;
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
    }

    private void SetPlayerControls(bool enabled)
    {
        if (_playerController == null) FindPlayer();
        if (_playerController != null)
        {
            _playerController.enabled = enabled;
        }

        if (_playerObject != null)
        {
            if (_playerObject.TryGetComponent<StarterAssets.ThirdPersonController>(out var tpc))
            {
                tpc.enabled = enabled;
            }
            if (_playerObject.TryGetComponent<PlayerCombat>(out var combat))
            {
                combat.enabled = enabled;
            }
        }
    }

    private void OnGUI()
    {
        int sw = Screen.width;
        int sh = Screen.height;

        switch (_currentState)
        {
            case MenuState.StartScreen:
                DrawStartScreen(sw, sh);
                break;

            case MenuState.Paused:
                DrawPauseScreen(sw, sh);
                break;

            case MenuState.DeathScreen:
                DrawDeathScreen(sw, sh);
                break;

            case MenuState.OptionsMenu:
                DrawOptionsBackButton(sw, sh);
                break;
        }
    }

    private void DrawStartScreen(int sw, int sh)
    {
        // 1. Full-screen horror city background
        if (_horrorCityBg == null) LoadHorrorCityBg();

        if (_horrorCityBg != null)
        {
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _horrorCityBg, ScaleMode.ScaleAndCrop);
        }

        // 2. Atmospheric dark vignette overlay for horror tone and UI readability
        GUI.backgroundColor = new Color(0.02f, 0.02f, 0.04f, 0.55f);
        GUI.Box(new Rect(0, 0, sw, sh), "");

        // 3. Central Title & Menu Panel
        float panelW = 500f;
        float panelH = 430f;
        float panelX = (sw - panelW) * 0.5f;
        float panelY = (sh - panelH) * 0.5f;

        GUIStyle panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.normal.background = Texture2D.whiteTexture;
        GUI.backgroundColor = new Color(0.06f, 0.07f, 0.1f, 0.90f);
        GUI.Box(new Rect(panelX, panelY, panelW, panelH), "", panelStyle);

        // Title Drop Shadow & Title
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 34,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        // Shadow
        titleStyle.normal.textColor = Color.black;
        GUI.Label(new Rect(panelX + 2f, panelY + 32f, panelW, 45f), "🧟 ZOMBIE APOCALYPSE", titleStyle);
        // Foreground Bloody Red
        titleStyle.normal.textColor = new Color(1f, 0.2f, 0.15f);
        GUI.Label(new Rect(panelX, panelY + 30f, panelW, 45f), "🧟 ZOMBIE APOCALYPSE", titleStyle);

        // Subtitle
        GUIStyle subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Italic,
            alignment = TextAnchor.MiddleCenter
        };
        subStyle.normal.textColor = new Color(0.8f, 0.85f, 0.9f);
        GUI.Label(new Rect(panelX, panelY + 75f, panelW, 25f), "CITY SURVIVAL EXPERIENCE", subStyle);

        // Buttons
        GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        float btnW = 340f;
        float btnH = 52f;
        float btnX = panelX + (panelW - btnW) * 0.5f;
        float startY = panelY + 130f;
        float spacing = 68f;

        // Button 1: Start Game
        GUI.backgroundColor = new Color(0.12f, 0.65f, 0.25f, 1f);
        if (GUI.Button(new Rect(btnX, startY, btnW, btnH), "▶ START GAME", btnStyle))
        {
            StartGame();
        }

        // Button 2: Settings / Options (CitrioN SMC)
        GUI.backgroundColor = new Color(0.18f, 0.45f, 0.78f, 1f);
        if (GUI.Button(new Rect(btnX, startY + spacing, btnW, btnH), "⚙ SETTINGS / OPTIONS", btnStyle))
        {
            OpenOptionsMenu(MenuState.StartScreen);
        }

        // Button 3: Quit
        GUI.backgroundColor = new Color(0.72f, 0.16f, 0.16f, 1f);
        if (GUI.Button(new Rect(btnX, startY + spacing * 2, btnW, btnH), "✕ QUIT GAME", btnStyle))
        {
            QuitGame();
        }
    }

    private void DrawPauseScreen(int sw, int sh)
    {
        // Dark translucent overlay
        GUI.backgroundColor = new Color(0.02f, 0.02f, 0.04f, 0.85f);
        GUI.Box(new Rect(0, 0, sw, sh), "");

        // Menu Panel
        float panelW = 460f;
        float panelH = 450f;
        float panelX = (sw - panelW) * 0.5f;
        float panelY = (sh - panelH) * 0.5f;

        GUIStyle panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.normal.background = Texture2D.whiteTexture;
        GUI.backgroundColor = new Color(0.08f, 0.1f, 0.14f, 0.98f);
        GUI.Box(new Rect(panelX, panelY, panelW, panelH), "", panelStyle);

        // Title
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = Color.yellow;
        GUI.Label(new Rect(panelX, panelY + 25f, panelW, 40f), "⏸️ GAME PAUSED", titleStyle);

        // Buttons
        GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        float btnW = 320f;
        float btnH = 48f;
        float btnX = panelX + (panelW - btnW) * 0.5f;
        float startY = panelY + 90f;
        float spacing = 62f;

        // Button 1: Continue
        GUI.backgroundColor = new Color(0.15f, 0.6f, 0.25f, 1f);
        if (GUI.Button(new Rect(btnX, startY, btnW, btnH), "▶ CONTINUE", btnStyle))
        {
            ResumeGame();
        }

        // Button 2: Restart Game
        GUI.backgroundColor = new Color(0.85f, 0.5f, 0.1f, 1f);
        if (GUI.Button(new Rect(btnX, startY + spacing, btnW, btnH), "🔄 RESTART GAME", btnStyle))
        {
            RestartGame();
        }

        // Button 3: Settings / Options (CitrioN SMC)
        GUI.backgroundColor = new Color(0.2f, 0.45f, 0.75f, 1f);
        if (GUI.Button(new Rect(btnX, startY + spacing * 2, btnW, btnH), "⚙ SETTINGS / OPTIONS", btnStyle))
        {
            OpenOptionsMenu(MenuState.Paused);
        }

        // Button 4: Quit to Main Menu
        GUI.backgroundColor = new Color(0.65f, 0.15f, 0.15f, 1f);
        if (GUI.Button(new Rect(btnX, startY + spacing * 3, btnW, btnH), "🏠 QUIT TO MAIN MENU", btnStyle))
        {
            ReturnToMainMenu();
        }
    }

    private void DrawDeathScreen(int sw, int sh)
    {
        // Deep red/dark overlay
        GUI.backgroundColor = new Color(0.3f, 0.02f, 0.02f, 0.92f);
        GUI.Box(new Rect(0, 0, sw, sh), "");

        // Menu Panel
        float panelW = 460f;
        float panelH = 380f;
        float panelX = (sw - panelW) * 0.5f;
        float panelY = (sh - panelH) * 0.5f;

        GUIStyle panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.normal.background = Texture2D.whiteTexture;
        GUI.backgroundColor = new Color(0.12f, 0.06f, 0.06f, 0.98f);
        GUI.Box(new Rect(panelX, panelY, panelW, panelH), "", panelStyle);

        // Title
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 34,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = new Color(1f, 0.15f, 0.15f);
        GUI.Label(new Rect(panelX, panelY + 25f, panelW, 45f), "💀 YOU DIED", titleStyle);

        // Subtitle
        GUIStyle subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Italic,
            alignment = TextAnchor.MiddleCenter
        };
        subStyle.normal.textColor = new Color(0.85f, 0.8f, 0.8f);
        GUI.Label(new Rect(panelX, panelY + 70f, panelW, 25f), "The zombie outbreak claimed another soul", subStyle);

        // Buttons
        GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        float btnW = 320f;
        float btnH = 52f;
        float btnX = panelX + (panelW - btnW) * 0.5f;
        float startY = panelY + 120f;
        float spacing = 68f;

        // Button 1: Restart from beginning
        GUI.backgroundColor = new Color(0.15f, 0.65f, 0.25f, 1f);
        if (GUI.Button(new Rect(btnX, startY, btnW, btnH), "🔄 RESTART GAME", btnStyle))
        {
            RestartGame();
        }

        // Button 2: Quit to Main Menu
        GUI.backgroundColor = new Color(0.7f, 0.2f, 0.2f, 1f);
        if (GUI.Button(new Rect(btnX, startY + spacing, btnW, btnH), "🏠 QUIT TO MAIN MENU", btnStyle))
        {
            ReturnToMainMenu();
        }

        // Button 3: Quit game
        GUI.backgroundColor = new Color(0.35f, 0.35f, 0.4f, 1f);
        if (GUI.Button(new Rect(btnX, startY + spacing * 2, btnW, btnH), "✕ QUIT GAME", btnStyle))
        {
            QuitGame();
        }
    }

    private void DrawOptionsBackButton(int sw, int sh)
    {
        // In CitrioN Options menu, draw a clean floating Back button at the bottom center
        GUIStyle backBtnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        GUI.backgroundColor = new Color(0.85f, 0.25f, 0.25f, 1f);
        float btnW = 200f;
        float btnH = 42f;
        if (GUI.Button(new Rect((sw - btnW) * 0.5f, sh - 60f, btnW, btnH), "← BACK TO MENU", backBtnStyle))
        {
            CloseOptionsMenu();
        }
    }
}
