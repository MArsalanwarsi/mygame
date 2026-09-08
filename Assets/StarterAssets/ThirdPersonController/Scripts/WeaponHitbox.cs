using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class WeaponHitbox : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private BoxCollider _hitboxCollider;
    [SerializeField] private Animator _playerAnimator;

    [Header("Distinct Attack Damage Values")]
    [SerializeField] private int _combo1Damage = 25;
    [SerializeField] private int _combo2Damage = 35;
    [SerializeField] private int _heavyAttackDamage = 60;
    [SerializeField] private int _defaultDamage = 25;

    private readonly HashSet<Collider> _hitTargets = new();
    private bool _isHeavyAttack = false;

    private void Awake()
    {
        if (_hitboxCollider == null) _hitboxCollider = GetComponent<BoxCollider>();
        if (_playerAnimator == null) _playerAnimator = GetComponentInParent<Animator>();

        _hitboxCollider.isTrigger = true;
        _hitboxCollider.enabled = false;
    }

    public void BeginAttackHit()
    {
        BeginAttackHit(_isHeavyAttack);
    }

    public void BeginAttackHit(bool isHeavy)
    {
        _isHeavyAttack = isHeavy;
        _hitTargets.Clear();
        _hitboxCollider.enabled = true;
    }

    public void BeginAttackHit(int isHeavyInt)
    {
        BeginAttackHit(isHeavyInt > 0);
    }

    public void BeginAttackHit(string tag)
    {
        BeginAttackHit(tag == "Heavy" || _isHeavyAttack);
    }

    public void EndAttackHit()
    {
        _hitboxCollider.enabled = false;
        _hitTargets.Clear();
        _isHeavyAttack = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_hitboxCollider.enabled) return;

        // Prevent striking ourselves if the blade hits our own player capsule
        if (other.transform.root == transform.root) return;

        Health targetHealth = other.GetComponent<Health>();
        if (targetHealth == null) targetHealth = other.GetComponentInParent<Health>();

        if (targetHealth == null) return;
        if (!_hitTargets.Add(other)) return;

        int calculatedDamage = DetermineCurrentAttackDamage();
        targetHealth.TakeDamage(calculatedDamage);
    }

    private int DetermineCurrentAttackDamage()
    {
        if (_isHeavyAttack) return _heavyAttackDamage;

        if (_playerAnimator != null)
        {
            // Checks layer 1 (CombatLayer) in your Animator
            AnimatorStateInfo stateInfo = _playerAnimator.GetCurrentAnimatorStateInfo(1);

            if (stateInfo.IsTag("Heavy")) return _heavyAttackDamage;
            if (stateInfo.IsTag("Combo2")) return _combo2Damage;
            if (stateInfo.IsTag("Combo1")) return _combo1Damage;
        }

        return _defaultDamage;
    }

    private void OnDisable()
    {
        if (_hitboxCollider != null) _hitboxCollider.enabled = false;
        _hitTargets.Clear();
    }
}
