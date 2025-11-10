using UnityEngine;
using TMPro;
using System;

public class UIController : MonoBehaviour
{
    public GameManager gm;              // optional: 씬의 GameManager 할당
    public TextMeshProUGUI tText;       // 타이머
    public TextMeshProUGUI sText;       // 점수
    public TextMeshProUGUI aText;       // 탄약

    WeaponController boundWeapon;
    bool eventBound = false;

    void Start()
    {
        if (gm == null) gm = FindFirstObjectByType<GameManager>();
        ForceUpdateAllUI();
    }

    void Update()
    {
        UpdateTimer();
        UpdateScore();
        // Ammo는 이벤트 우선. 이벤트 없다면 폴링(안전)
        if (!eventBound) UpdateAmmoPolling();
    }

    public void BindToWeapon(WeaponController w)
    {
        if (boundWeapon == w) return;
        UnbindWeapon();
        boundWeapon = w;
        if (boundWeapon != null)
        {
            try
            {
                boundWeapon.OnAmmoChanged.AddListener(OnAmmoChanged);
                eventBound = true;
                OnAmmoChanged(boundWeapon.curAmmo, boundWeapon.data.magSize, boundWeapon.reserveMags);
            }
            catch (Exception)
            {
                eventBound = false;
            }
        }
    }

    public void UnbindWeapon()
    {
        if (boundWeapon != null && eventBound)
        {
            try { boundWeapon.OnAmmoChanged.RemoveListener(OnAmmoChanged); } catch { }
        }
        boundWeapon = null;
        eventBound = false;
    }

    void OnDestroy() { UnbindWeapon(); }

    void UpdateTimer()
    {
        if (gm == null || tText == null) return;
        int sec = Mathf.Max(0, Mathf.CeilToInt(gm.timeLeft));
        int min = sec / 60;
        int s = sec % 60;
        tText.text = string.Format("{0:00}:{1:00}", min, s);
    }

    void UpdateScore()
    {
        if (gm == null || sText == null) return;
        sText.text = gm.totalScore.ToString();
    }

    void UpdateAmmoPolling()
    {
        if (aText == null) return;
        if (boundWeapon != null)
        {
            aText.text = $"{boundWeapon.curAmmo} / {boundWeapon.data.magSize} ({boundWeapon.reserveMags})";
            return;
        }
        aText.text = "- / -";
    }

    void OnAmmoChanged(int cur, int mag, int reserve)
    {
        if (aText == null) return;
        aText.text = $"{cur} / {mag} ({reserve})";
    }

    public void ForceUpdateAllUI()
    {
        UpdateTimer();
        UpdateScore();
        UpdateAmmoPolling();
    }
}