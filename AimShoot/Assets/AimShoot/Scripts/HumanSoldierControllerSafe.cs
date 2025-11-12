using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace KevinIglesias
{
    [DisallowMultipleComponent]
    public class HumanSoldierControllerSafe : MonoBehaviour
    {
        [Header("References")]
        public Animator animator;
        [Tooltip("원본 스크립트의 weapons 배열과 동일한 순서로 프리팹/오브젝트를 넣어주세요")]
        public GameObject[] weapons;

        [Header("State (Inspector read-only)")]
        public SoldierWeapons equippedWeapon = SoldierWeapons.None;
        public SoldierPosition position = SoldierPosition.StandUp;
        public SoldierAction action = SoldierAction.Nothing;
        public SoldierMovement movement = SoldierMovement.NoMovement;

        [Header("Options")]
        public bool autoAssignAnimator = true;
        [Tooltip("애니메이터 파라미터가 존재하지 않을 때 경고를 콘솔에 남깁니다.")]
        public bool warnMissingParameter = true;
        [Tooltip("ChangeWeapons 동작 시 코루틴 대기 시간")]
        public float changeWeaponInterval = 1.5f;

        [Header("Events (optional)")]
        public UnityEvent onWeaponChanged;
        public UnityEvent onActionTriggered;

        // 내부
        private Coroutine changingWeaponsCoroutine = null;
        private int currentWeaponIndex = 0;

        void Reset()
        {
            // 편의: 컴포넌트가 새로 추가될 때 Animator 자동 할당 시도
            if (animator == null && autoAssignAnimator)
                animator = GetComponent<Animator>();
        }

        void Awake()
        {
            if (animator == null && autoAssignAnimator)
            {
                animator = GetComponent<Animator>();
                if (animator == null && warnMissingParameter)
                    Debug.LogWarning($"[{nameof(HumanSoldierControllerSafe)}] Animator가 없습니다: {name}");
            }
        }

        void Update()
        {
            // 안전: animator가 없으면 트리거 호출을 시도하지 않음(필요시 로그)
            if (animator != null)
            {
                SafeSetTrigger(equippedWeapon.ToString());
                SafeSetTrigger(position.ToString());

                if (action != SoldierAction.Nothing && action != SoldierAction.ChangeWeapons)
                {
                    SafeSetTrigger(action.ToString());
                    onActionTriggered?.Invoke();
                }

                SafeSetTrigger(movement.ToString());
            }

            // ChangeWeapons 제어
            if (action == SoldierAction.ChangeWeapons)
            {
                if (changingWeaponsCoroutine == null)
                    changingWeaponsCoroutine = StartCoroutine(ChangingWeaponsCoroutine());
            }
            else
            {
                if (changingWeaponsCoroutine != null)
                {
                    StopCoroutine(changingWeaponsCoroutine);
                    changingWeaponsCoroutine = null;
                }
            }
        }

        IEnumerator ChangingWeaponsCoroutine()
        {
            while (true)
            {
                AdvanceWeaponIndex();
                PlayUnsheatheForCurrentWeapon();
                onWeaponChanged?.Invoke();
                yield return new WaitForSeconds(Mathf.Max(0.1f, changeWeaponInterval));
            }
        }

        void AdvanceWeaponIndex()
        {
            currentWeaponIndex++;
            if (currentWeaponIndex >= 5) currentWeaponIndex = 0; // 0..4 대응
        }

        void PlayUnsheatheForCurrentWeapon()
        {
            // UnsheatheWeapons enum 순서가 0..4이고 currentWeaponIndex 범위와 일치한다고 가정
            var unsheatheName = ((UnsheatheWeapons)currentWeaponIndex).ToString();
            SafeSetTrigger(unsheatheName);
        }

        // 안전한 SetTrigger 호출: 파라미터 존재 확인 후 SetTrigger
        void SafeSetTrigger(string triggerName)
        {
            if (string.IsNullOrEmpty(triggerName) || animator == null) return;

            if (!HasTriggerParameter(triggerName))
            {
                if (warnMissingParameter)
                    Debug.LogWarning($"[{nameof(HumanSoldierControllerSafe)}] Animator에 트리거 파라미터가 없습니다: '{triggerName}' (오브젝트: {name})");
                return;
            }

            // Reset은 선택적: 애니메이터 설정에 따라 제거 가능
            animator.ResetTrigger(triggerName);
            animator.SetTrigger(triggerName);
        }

        bool HasTriggerParameter(string paramName)
        {
            if (animator == null || string.IsNullOrEmpty(paramName)) return false;
            foreach (var p in animator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == paramName)
                    
                    return true;
            }
            return false;
        }

        // public API: 안전한 무기 변경(원본 ChangeWeapon과 유사하지만 방어적)
        public void ChangeWeaponSafe(SoldierWeapons newWeapon)
        {
            if (weapons == null || weapons.Length == 0)
            {
                Debug.LogWarning($"[{nameof(HumanSoldierControllerSafe)}] weapons 배열이 비어있습니다: {name}");
                return;
            }

            int newIndex = (int)newWeapon - 1;
            if (newIndex < 0 || newIndex >= weapons.Length)
            {
                Debug.LogWarning($"[{nameof(HumanSoldierControllerSafe)}] 잘못된 무기 인덱스: {newWeapon}");
                return;
            }

            // 모두 끄기
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] != null) weapons[i].SetActive(false);
            }

            // 주무기 켜기
            if (weapons[newIndex] != null) weapons[newIndex].SetActive(true);

            // DualGun 예외 처리: Gun을 함께 활성화
            if (newWeapon == SoldierWeapons.DualGun)
            {
                int gunIndex = (int)SoldierWeapons.Gun - 1;
                if (gunIndex >= 0 && gunIndex < weapons.Length && weapons[gunIndex] != null)
                    weapons[gunIndex].SetActive(true);
            }

            equippedWeapon = newWeapon;
            onWeaponChanged?.Invoke();
        }
    }
}