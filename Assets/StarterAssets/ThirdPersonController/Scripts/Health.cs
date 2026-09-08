using UnityEngine;
using UnityEngine.Events;
using TMPro; // Crucial: Gives access to TextMeshPro code tools

public sealed class Health : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int _maxHealth = 100;
    private int _currentHealth;

    [Header("UI Dependencies (Optional)")]
    [SerializeField] private UnityEngine.UI.Slider _healthSlider;
    [SerializeField] private TextMeshProUGUI _healthTextDisplay;
    [SerializeField] private RectTransform _fillRect;
    [SerializeField] private UnityEngine.UI.Image _fillImage;

    [Header("Events")]
    public UnityEvent<float> OnHealthChanged = new UnityEvent<float>();
    public UnityEvent OnDeath = new UnityEvent();

    public int CurrentHealth => _currentHealth;
    public int MaxHealth => _maxHealth;

    private void Awake()
    {
        bool isPlayer = CompareTag("Player") || gameObject.name.Contains("Player") || GetComponent<StarterAssets.ThirdPersonController>() != null;
        if (isPlayer && _maxHealth < 500)
        {
            _maxHealth = 500;
        }

        _currentHealth = _maxHealth;
        FindUIComponents();
        UpdateTextUI();
    }

    private void Start()
    {
        FindUIComponents();
        UpdateTextUI();
    }

    private void FindUIComponents()
    {
        bool isPlayer = CompareTag("Player") || gameObject.name.Contains("Player") || GetComponent<StarterAssets.ThirdPersonController>() != null;
        if (!isPlayer) return;

        if (_healthSlider == null)
        {
            var sliders = FindObjectsByType<UnityEngine.UI.Slider>(FindObjectsInactive.Include);
            foreach (var s in sliders)
            {
                if (s.name.ToLower().Contains("player") || s.name.ToLower().Contains("health"))
                {
                    _healthSlider = s;
                    break;
                }
            }
        }

        if (_healthSlider != null)
        {
            if (_fillRect == null)
            {
                Transform fillT = _healthSlider.transform.Find("Fill Area/Fill");
                if (fillT == null) fillT = _healthSlider.transform.Find("Fill");
                if (fillT == null)
                {
                    var images = _healthSlider.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                    foreach (var img in images)
                    {
                        if (img.gameObject.name.ToLower().Contains("fill"))
                        {
                            fillT = img.transform;
                            break;
                        }
                    }
                }

                if (fillT != null)
                {
                    _fillRect = fillT.GetComponent<RectTransform>();
                    _healthSlider.fillRect = _fillRect;
                }
            }
        }

        if (_fillRect == null)
        {
            var fillObj = GameObject.Find("Fill");
            if (fillObj != null)
            {
                _fillRect = fillObj.GetComponent<RectTransform>();
                _fillImage = fillObj.GetComponent<UnityEngine.UI.Image>();
                if (_healthSlider != null && _healthSlider.fillRect == null)
                {
                    _healthSlider.fillRect = _fillRect;
                }
            }
        }

        if (_healthTextDisplay == null)
        {
            var tmps = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include);
            foreach (var t in tmps)
            {
                if (t.name.ToLower().Contains("health"))
                {
                    _healthTextDisplay = t;
                    break;
                }
            }
        }
    }

    public void TakeDamage(int damageAmount)
    {
        if (_currentHealth <= 0) return;

        _currentHealth -= damageAmount;
        _currentHealth = Mathf.Clamp(_currentHealth, 0, _maxHealth);

        // Update Slider UI
        float healthNormalized = (float)_currentHealth / _maxHealth;
        OnHealthChanged?.Invoke(healthNormalized);

        // Update Text UI & Bar
        UpdateTextUI();

        Debug.Log($"[Health] {gameObject.name} took {damageAmount} damage! Current HP: {_currentHealth}/{_maxHealth}");

        if (_currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int healAmount)
    {
        if (_currentHealth <= 0) return;

        _currentHealth += healAmount;
        _currentHealth = Mathf.Clamp(_currentHealth, 0, _maxHealth);

        // Update Slider UI
        float healthNormalized = (float)_currentHealth / _maxHealth;
        OnHealthChanged?.Invoke(healthNormalized);

        // Update Text UI & Bar
        UpdateTextUI();

        Debug.Log($"💚 [Health] {gameObject.name} recovered {healAmount} HP! Current HP: {_currentHealth}/{_maxHealth}");
    }

    public void SetMaxHealth(int newMaxHealth, bool refillHealth = true)
    {
        _maxHealth = newMaxHealth;
        if (refillHealth)
        {
            _currentHealth = _maxHealth;
        }
        else
        {
            _currentHealth = Mathf.Min(_currentHealth, _maxHealth);
        }

        float healthNormalized = _maxHealth > 0 ? (float)_currentHealth / _maxHealth : 0f;
        OnHealthChanged?.Invoke(healthNormalized);
        UpdateTextUI();
        Debug.Log($"[Health] {gameObject.name} max health set to {_maxHealth}. Current HP: {_currentHealth}");
    }

    private void UpdateTextUI()
    {
        float ratio = Mathf.Clamp01((float)_currentHealth / _maxHealth);

        if (_healthTextDisplay != null)
        {
            _healthTextDisplay.text = $"{_currentHealth}/{_maxHealth}";
        }

        if (_healthSlider != null)
        {
            _healthSlider.value = ratio;
            if (_healthSlider.fillRect == null && _fillRect != null)
            {
                _healthSlider.fillRect = _fillRect;
            }
        }

        // Direct anchor resizing on the green fill rect for guaranteed visual shrinkage
        if (_fillRect != null)
        {
            _fillRect.anchorMin = new Vector2(0f, 0f);
            _fillRect.anchorMax = new Vector2(ratio, 1f);
            _fillRect.offsetMin = Vector2.zero;
            _fillRect.offsetMax = Vector2.zero;
        }

        // Shift color from vibrant green to yellow and red as health drops
        if (_fillImage != null)
        {
            if (ratio > 0.5f)
            {
                _fillImage.color = Color.Lerp(Color.yellow, new Color(0.188f, 0.717f, 0f), (ratio - 0.5f) * 2f);
            }
            else
            {
                _fillImage.color = Color.Lerp(new Color(0.85f, 0.1f, 0.1f), Color.yellow, ratio * 2f);
            }
        }
    }

    public void ResetHealth()
    {
        _currentHealth = _maxHealth;
        UpdateTextUI();
        OnHealthChanged?.Invoke(1.0f);

        bool isPlayer = CompareTag("Player") || gameObject.name.Contains("Player") || GetComponent<StarterAssets.ThirdPersonController>() != null;
        if (isPlayer)
        {
            if (TryGetComponent<Animator>(out Animator anim))
            {
                anim.ResetTrigger("Die");
                if (anim.layerCount > 1) anim.SetLayerWeight(1, 1f);
            }
            if (TryGetComponent<StarterAssets.ThirdPersonController>(out var tpc)) tpc.enabled = true;
            if (TryGetComponent<PlayerCombat>(out var combat)) combat.enabled = true;
            if (TryGetComponent<CharacterController>(out CharacterController cc)) cc.enabled = true;
        }
    }

    private void Die()
    {
        Debug.Log($"💀 {gameObject.name} has been destroyed!");
        OnDeath?.Invoke();

        // Check if this is the player
        bool isPlayer = CompareTag("Player") || gameObject.name.Contains("Player") || GetComponent<StarterAssets.ThirdPersonController>() != null;

        if (isPlayer)
        {
            // Mute upper-body combat layer so full-body death animation plays uninterrupted on Base Layer
            if (TryGetComponent<Animator>(out Animator animator))
            {
                if (animator.layerCount > 1) animator.SetLayerWeight(1, 0f);
                animator.SetTrigger("Die");
            }

            // Disable player control scripts so locomotion blend trees don't interrupt death or throw Move() on inactive CC
            if (TryGetComponent<StarterAssets.ThirdPersonController>(out var tpc)) tpc.enabled = false;
            if (TryGetComponent<PlayerCombat>(out var combat)) combat.enabled = false;
            if (TryGetComponent<CharacterController>(out CharacterController cc)) cc.enabled = false;

            if (GameMenuManager.Instance != null)
            {
                GameMenuManager.Instance.OnPlayerDied();
            }
            return;
        }

        // For non-player (zombies, monsters)
        if (TryGetComponent<Animator>(out Animator enemyAnim))
        {
            enemyAnim.SetTrigger("Die");
        }

        // Disable the physics collider immediately so dead bodies can't block the player
        if (TryGetComponent<Collider>(out Collider col)) col.enabled = false;

        // Safely clear the object from the world AFTER the animation finishes playing (e.g., 2.5 seconds)
        Destroy(gameObject, 2.5f);
    }
}
