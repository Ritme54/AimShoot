using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class GunController : MonoBehaviour
{
    [Header("Fire Settings")]
    public float fireRate = 0.25f;
    public float maxDistance = 100;
    public LayerMask targetLayerMask;
    public GameObject hitEffectPrefab;

    private float lastFireTime = -999f;

    public int currentAmmo = 12; // 현재 탄약 (초기값 샘플)
    public int maxAmmo = 12;     // 탄창 용량



    void Update()
    {
        HandleInput();

    }

    void HandleInput()
    {
        if (Input.GetMouseButton(0) && Time.time >= lastFireTime + fireRate)

        {
            Shoot();
            lastFireTime = Time.time;

        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            currentAmmo = maxAmmo; // 간단 리로드
        }


        void Shoot()
        {

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("메인카메라 설정 안됨");
                return;
            }

            if (currentAmmo <= 0)
            {
                // 탄약 부족 처리(재장전 소리/로직 또는 로그)
                Debug.Log("No ammo");
                return;
            }

            // 발사 성공 시 탄약 감소
            currentAmmo--;


            Ray ray = cam.ScreenPointToRay(Input.mousePosition);

            RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, targetLayerMask);

            if (hits.Length == 0)
            {
                Debug.DrawRay(ray.origin, ray.direction * maxDistance, Color.red, 0.5f);
                Debug.Log("Miss");
            }

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            RaycastHit selHit = hits[0];

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider.gameObject.layer == LayerMask.NameToLayer("Head"))
                {
                    selHit = hits[i];            // Head가 발견되면 선택하고 루프 종료
                    break;
                }
            }

            Collider hitCollider = selHit.collider;
            GameObject hitObj = hitCollider.gameObject;

            if (hitEffectPrefab != null)
            {
                // 필요 시 Quaternion.LookRotation(selHit.normal) 등으로 이펙트 정렬 가능
                Instantiate(hitEffectPrefab, selHit.point, Quaternion.identity);
            }

            // 디버그 로그 (기존과 유사하되 보다 명확한 정보 표기)
            Debug.Log($"Hit : {hitObj.name} at {selHit.point} (Collider: {hitCollider.name})");

            var target = hitObj.GetComponentInParent<Targets>();                 // 기존에 사용하신 이름이 Targets라면 이것으로 동작


            if (target == null && hitObj.transform.parent != null)
            {
                // 머리 콜라이더가 자식일 경우 부모에서 타깃 컴포넌트를 찾음
                target = hitObj.transform.parent.GetComponent<Targets>();
            }

            if (target != null)
            {
                // 헤드샷 여부 판정: 맞은 콜라이더가 Head 레이어에 속하는지로 판단
                bool isHead = (hitCollider.gameObject.layer == LayerMask.NameToLayer("Head"));

                // 기존 CalculateDamage 함수 호출(함수명 및 내부는 변경하지 않음)
                int dmg = CalculateDamage(isHead);

                // 기존 Target(s) 인터페이스에 맞춰 OnHit 호출 (기존 변수/함수명 유지)
                target.OnHit(isHead, dmg);
            }
            else
            {
                // Targets 컴포넌트가 전혀 없을 때의 디버그 메시지 (디버깅 용)
                Debug.Log($"Hit collider {hitCollider.name} but no Targets component found on object or parent.");
            }
        }

        // CalculateDamage(): 기존 함수명 및 내부 로직 구조 유지(단, 지역변수명은 짧게 유지)
        int CalculateDamage(bool isHead)
        {
            int baseDamage = 25;                   // 기존 기본 데미지 유지
            if (isHead) return Mathf.CeilToInt(baseDamage * 1.5f); // 헤드샷 보정(1.5배)
            return baseDamage;
        }
    }
}
