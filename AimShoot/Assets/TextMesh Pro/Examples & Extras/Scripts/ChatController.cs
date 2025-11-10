using UnityEngine;
using TMPro;
using System;

public class ChatController : MonoBehaviour
{
    public TMP_InputField input;
    public Transform contentParent;
    public GameObject linePrefab;           // 텍스트 한 줄 프리팹(TextMeshProUGUI 포함)

    public WeaponManager wm;                // 명령 연동(예: /equip)
    public SpawnManager sp;                 // 명령 연동(예: /spawn)
    public GameManager gm;                  // /time /score 등

    void Start()
    {
        if (input != null) input.onSubmit.AddListener(OnSubmit);
    }

    void OnSubmit(string txt)
    {
        if (string.IsNullOrWhiteSpace(txt)) return;
        PostLine("Player: " + txt);
        ParseCommand(txt.Trim());
        input.text = "";
        input.ActivateInputField();
    }

    void PostLine(string line)
    {
        if (linePrefab != null && contentParent != null)
        {
            var go = Instantiate(linePrefab, contentParent);
            var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = line;
        }
        else
        {
            Debug.Log("[Chat] " + line);
        }
    }

    void ParseCommand(string txt)
    {
        if (!txt.StartsWith("/")) return; // 일반 채팅
        var parts = txt.Substring(1).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        var cmd = parts[0].ToLower();

        switch (cmd)
        {
            case "equip":
                if (parts.Length >= 2)
                {
                    var arg = parts[1].ToLower();
                    if (arg == "primary") wm?.Equip(wm.primary);
                    else if (arg == "secondary") wm?.Equip(wm.secondary);
                    PostLine($"Command: equip {arg}");
                }
                break;
            case "giveammo":
                // 예: /giveammo 2 -> reserveMags +2 for current weapon
                if (parts.Length >= 2 && int.TryParse(parts[1], out int n))
                {
                    var cur = wm?.GetCurrent();
                    if (cur != null)
                    {
                        cur.reserveMags += n;
                        cur.BroadcastAmmo(); // internal method is private; if inaccessible, use public API or UI update
                        PostLine($"Added {n} mags to {cur.data.name}");
                    }
                }
                break;
            case "spawn":
                if (sp != null && parts.Length >= 2 && int.TryParse(parts[1], out int cnt))
                {
                    sp.SpawnMany(cnt);
                    PostLine($"Spawned {cnt} targets");
                }
                break;
            case "time":
                if (parts.Length >= 2 && float.TryParse(parts[1], out float tm) && gm != null)
                {
                    gm.timeLeft = tm;
                    PostLine($"Set time to {tm}");
                }
                break;
            default:
                PostLine($"Unknown command: {cmd}");
                break;
        }
    }
}