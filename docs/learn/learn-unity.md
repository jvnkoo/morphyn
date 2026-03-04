# Learn Morphyn Unity Bridge in Y Minutes

```cs
using UnityEngine;
using Morphyn.Unity;
using Morphyn.Parser;

// ── SETUP ─────────────────────────────────────────────────────────────────────
// 1. Add MorphynController component to a GameObject in your scene
// 2. Drag .morph files into the Morphyn Scripts array
// 3. Press Play

MorphynController morphyn = MorphynController.Instance;


// ── READ FIELDS ───────────────────────────────────────────────────────────────

// Typed helpers — cleanest option when you know the type
float hp        = morphyn.GetFloat("Player", "hp");
float hp        = morphyn.GetFloat("Player", "hp", 100f); // with default
bool alive      = morphyn.GetBool("Player", "alive");
double speed    = morphyn.GetDouble("Player", "speed");
string nickname = morphyn.GetString("Player", "nickname", "unknown");

// Generic — auto-converts to T
float hp   = morphyn.Get<float>("Player", "hp", 100f);
bool alive = morphyn.Get<bool>("Player", "alive");
int level  = morphyn.Get<int>("Player", "level");

// Dynamic — returns whatever is stored (double, bool, string, MorphynPool or null)
object val = morphyn.Get("Player", "hp");

// Raw MorphynValue struct
MorphynValue raw = morphyn.GetField("Player", "hp");
if (!raw.IsNull) float hp = System.Convert.ToSingle(raw.ToObject());

// All fields at once
Dictionary<string, MorphynValue> fields = morphyn.GetAllFields("Player");


// ── WRITE FIELDS ──────────────────────────────────────────────────────────────

// Overloads accept primitives directly — no wrapping needed
morphyn.SetField("Player", "hp",    50.0);
morphyn.SetField("Player", "alive", true);
morphyn.SetField("Player", "name",  "Hero");
morphyn.SetField("Player", "speed", 3.5f);

// Or pass MorphynValue explicitly
morphyn.SetField("Player", "hp", MorphynValue.FromDouble(50.0));


// ── TRIGGER EVENTS ────────────────────────────────────────────────────────────

morphyn.Emit("Player", "damage", 25);           // fire and forget
morphyn.Emit("Player", "heal", 20);
morphyn.Emit("Enemy", "take_damage", 10, true); // multiple args


// ── SYNC EMIT (returns a value) ───────────────────────────────────────────────
// Executes immediately and returns the last assigned value inside the event

// Primitive overloads
MorphynValue result = morphyn.EmitSync("MathLib", "clamp", 150.0);

// Multiple args via MorphynValue
MorphynValue result = morphyn.EmitSync("MathLib", "clamp",
    MorphynValue.FromDouble(150.0),
    MorphynValue.FromDouble(0.0),
    MorphynValue.FromDouble(100.0));

float clamped = System.Convert.ToSingle(result.ToObject()); // 100


// ── C# LISTENERS ─────────────────────────────────────────────────────────────
// Subscribe a C# method to a Morphyn entity event.
// Handler receives event args as object?[].

Action<object?[]> onDeath = args => {
    Debug.Log("Player died");
    deathScreen.SetActive(true);
};

morphyn.On("Player", "death", onDeath);

morphyn.On("Player", "levelUp", args => {
    int level = System.Convert.ToInt32(args[0]);
    levelUpUI.Show(level);
});

// Always unsubscribe in OnDestroy to avoid memory leaks
morphyn.Off("Player", "death", onDeath);

public class MyComponent : MonoBehaviour
{
    void Start()
    {
        MorphynController.Instance.On("Player", "death", OnPlayerDeath);
    }

    void OnPlayerDeath(object?[] args) { /* ... */ }

    void OnDestroy()
    {
        MorphynController.Instance.Off("Player", "death", OnPlayerDeath);
    }
}


// ── MORPHYN-TO-MORPHYN SUBSCRIPTIONS FROM C# ─────────────────────────────────
// Equivalent to writing `when Player.death : onPlayerDeath` in .morph

morphyn.Subscribe("Logger", "Player", "death", "onPlayerDeath");
// subscriberEntity, targetEntity, targetEvent, handlerEvent

morphyn.Unsubscribe("Logger", "Player", "death", "onPlayerDeath");

// On vs Subscribe:
// On / Off      — C# method reacts to Morphyn event
// Subscribe     — Morphyn entity reacts to another Morphyn entity's event


// ── UNITY BRIDGE: call Unity from .morph ─────────────────────────────────────
// In .morph:
//   emit unity("PlaySound", "explosion")
//   emit unity("SpawnVFX", x, y, z)

// Register callbacks BEFORE MorphynController loads (use Awake, not Start)
[DefaultExecutionOrder(-100)]
public class Setup : MonoBehaviour
{
    void Awake()
    {
        UnityBridge.Instance.RegisterCallback("PlaySound", args => {
            string clip = args[0]?.ToString() ?? "";
            AudioSource.PlayClipAtPoint(Resources.Load<AudioClip>(clip), Vector3.zero);
        });

        UnityBridge.Instance.RegisterCallback("SpawnVFX", args => {
            float x = System.Convert.ToSingle(args[0]);
            float y = System.Convert.ToSingle(args[1]);
            float z = System.Convert.ToSingle(args[2]);
            Instantiate(vfxPrefab, new Vector3(x, y, z), Quaternion.identity);
        });
    }
}

// Built-in callbacks registered by MorphynController automatically:
//   emit unity("Log", "msg")        → Debug.Log
//   emit unity("Move", x, y, z)     → moves MorphynController's transform
//   emit unity("Rotate", angle)     → rotates MorphynController's transform


// ── SAVE / LOAD ───────────────────────────────────────────────────────────────

morphyn.SaveState();            // saves all entities to persistentDataPath/MorphynData/
morphyn.LoadState("Player");    // loads fields for one entity
morphyn.LoadAllStates();        // loads all

// Per-file save policy — set in Inspector on each MorphynScriptEntry:
//   None       — never save/load automatically
//   Auto       — load on startup, save on quit
//   ManualOnly — only when you call SaveState() / LoadState() from code

// Saved files are plain .morph — human-readable, version-controllable:
// entity Player {
//   has hp: 75
//   has level: 5
// }


// ── HOT RELOAD ────────────────────────────────────────────────────────────────
// Enable "Enable Hot Reload" on MorphynController (Editor only)
// Edit any .morph file → save → logic updates instantly in running game
// Entity field values are preserved across reloads


// ── FULL EXAMPLE ──────────────────────────────────────────────────────────────

// enemy.morph
/*
entity Enemy {
  has hp: 50
  has reward: 100

  event take_damage(amount) {
    hp - amount -> hp
    check hp <= 0: emit self.die
  }

  event die {
    emit unity("OnEnemyDied", reward)
    emit self.destroy
  }
}
*/

public class EnemyController : MonoBehaviour
{
    void Start()
    {
        MorphynController.Instance.On("Enemy", "die", args => {
            int reward = System.Convert.ToInt32(args[0]);
            ScoreManager.Add(reward);
            Destroy(gameObject);
        });
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Bullet"))
            MorphynController.Instance.Emit("Enemy", "take_damage", 25);
    }

    void OnDestroy()
    {
        MorphynController.Instance.Off("Enemy", "die", myHandler);
    }
}
```