using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    public WeaponController primary;
    public WeaponController secondary;
    public UIController ui;             // 바인딩 시 사용(인스펙터 연결)

    WeaponController cur;

    void Start()
    {
        Equip(primary);

    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) Equip(primary);
        if (Input.GetKeyDown(KeyCode.Alpha2)) Equip(secondary);

        // 발사/재장전 입력 전달
        if (cur != null)
        {
            if (cur.data.isAuto)
            {
                if (Input.GetMouseButton(0)) cur.TryFire();
            }
            else
            {
                if (Input.GetMouseButtonDown(0)) cur.TryFire();
            }

            if (Input.GetKeyDown(KeyCode.R)) cur.StartReload();
        }
    }

    public void Equip(WeaponController w)
    {
        if (w == null) return;
        if (cur == w) return;

        if (cur != null)
        {
            cur.OnUnequip();
            cur.gameObject.SetActive(false);
        }

        cur = w;
        cur.gameObject.SetActive(true);

        // UI 재바인딩
        if (ui != null) ui.BindToWeapon(cur);

        // Crosshair 갱신(선택)
        var cross = FindFirstObjectByType<CrosshairController>();
        if (cross != null) cross.SetCrosshair(cur.data.weaponType);
    }

    public WeaponController GetCurrent() => cur;
}