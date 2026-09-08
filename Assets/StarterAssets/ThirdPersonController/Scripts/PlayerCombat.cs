using StarterAssets;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(StarterAssetsInputs))]
public sealed class PlayerCombat : MonoBehaviour
{
    // Performance optimization: cache parameter hashes into memory strings
    private static readonly int Attack1Hash = Animator.StringToHash("Attack1");
    private static readonly int Attack2Hash = Animator.StringToHash("Attack2");
    private static readonly int Attack3Hash = Animator.StringToHash("Attack3");

    [Header("Dependencies")]
    [SerializeField] private Animator _animator;
    [SerializeField] private StarterAssetsInputs _input;

    [Header("Hitbox Link")]
    [SerializeField] private WeaponHitbox _weaponHitbox;

    private void Awake()
    {
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_input == null) _input = GetComponent<StarterAssetsInputs>();
        if (_weaponHitbox == null) _weaponHitbox = GetComponentInChildren<WeaponHitbox>();
    }

    private void Update()
    {
        if (GameMenuManager.Instance != null && GameMenuManager.Instance.CurrentState != GameMenuManager.MenuState.Playing) return;

        HandleCombatHotkeys();
    }

    private Coroutine _hitboxRoutine;

    private bool _isCurrentAttackHeavy;

    private void HandleCombatHotkeys()
    {
        bool rmbPressed = false;
        bool lmbPressed = false;

        // 1. StarterAssets input booleans
        if (_input != null)
        {
            if (_input.attack2 || _input.attack3)
            {
                rmbPressed = true;
                _input.attack2 = false;
                _input.attack3 = false;
            }

            if (_input.attack1)
            {
                lmbPressed = true;
                _input.attack1 = false;
            }
        }

        // 2. Direct device polling
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            if (UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame)
            {
                rmbPressed = true;
            }
            if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            {
                lmbPressed = true;
            }
        }

        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            if (UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
            {
                rmbPressed = true;
            }
            if (UnityEngine.InputSystem.Keyboard.current.fKey.wasPressedThisFrame)
            {
                lmbPressed = true;
            }
        }
#else
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.E))
        {
            rmbPressed = true;
        }
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.F))
        {
            lmbPressed = true;
        }
#endif

        // Execute Right-Click Heavy Attack
        if (rmbPressed)
        {
            _isCurrentAttackHeavy = true;
            if (_animator != null)
            {
                _animator.ResetTrigger(Attack1Hash);
                _animator.ResetTrigger(Attack2Hash);
                _animator.SetTrigger(Attack3Hash);
            }
            StartAttackHitbox(0.35f, 0.7f, isHeavy: true);
            return;
        }

        // Execute Left-Click Light Combo Attack
        if (lmbPressed)
        {
            _isCurrentAttackHeavy = false;
            if (_animator != null)
            {
                _animator.ResetTrigger(Attack2Hash);
                _animator.ResetTrigger(Attack3Hash);
                _animator.SetTrigger(Attack1Hash);
            }
            StartAttackHitbox(0.2f, 0.5f, isHeavy: false);
            return;
        }
    }

    private void StartAttackHitbox(float startDelay, float activeDuration, bool isHeavy)
    {
        if (_hitboxRoutine != null) StopCoroutine(_hitboxRoutine);
        _hitboxRoutine = StartCoroutine(HitboxWindow(startDelay, activeDuration, isHeavy));
    }

    private System.Collections.IEnumerator HitboxWindow(float delay, float duration, bool isHeavy)
    {
        yield return new WaitForSeconds(delay);
        BeginAttackHit();
        yield return new WaitForSeconds(duration);
        EndAttackHit();
        _hitboxRoutine = null;
    }

    // --- ANIMATION EVENT ROUTERS ---
    // Primary parameterless method: strictly complies with Unity AnimationEvent dispatch (0 parameters)
    public void BeginAttackHit()
    {
        if (_weaponHitbox != null) _weaponHitbox.BeginAttackHit(_isCurrentAttackHeavy);
    }

    // Supported parameter types in Unity AnimationEvents (int, string, bool via code)
    public void BeginAttackHit(int isHeavyInt)
    {
        if (_weaponHitbox != null) _weaponHitbox.BeginAttackHit(isHeavyInt > 0);
    }

    public void BeginAttackHit(string tag)
    {
        if (_weaponHitbox != null) _weaponHitbox.BeginAttackHit(tag == "Heavy" || _isCurrentAttackHeavy);
    }

    public void EndAttackHit()
    {
        if (_weaponHitbox != null) _weaponHitbox.EndAttackHit();
    }
}
