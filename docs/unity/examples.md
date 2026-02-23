# Unity Examples

## Shop with Discount Logic

**shop.morphyn:**
```morphyn
entity Shop {
  has swordPrice: 100
  has potionPrice: 20
  has armorPrice: 150
  
  on apply_sale(percent) {
    swordPrice - 50 -> swordPrice
    potionPrice - 10 -> potionPrice
    armorPrice - 75 -> armorPrice
    emit unity("Log", "SALE! 50% OFF!")
  }
}
```

**ShopUI.cs:**
```cs
using UnityEngine;
using UnityEngine.UI;

public class ShopUI : MonoBehaviour
{
    [SerializeField] private Text swordPriceText;
    
    void Start()
    {
        UpdatePrices();
    }
    
    void UpdatePrices()
    {
        double price = System.Convert.ToDouble(
            MorphynController.Instance.GetField("Shop", "swordPrice")
        );
        swordPriceText.text = $"Sword: ${price}";
    }
    
    public void OnSaleButtonClick()
    {
        MorphynController.Instance.Emit("Shop", "apply_sale", 50);
        UpdatePrices(); // Shows 50
    }
}
```

## Inventory with Capacity

**inventory.morphyn:**
```morphyn
entity Inventory {
  has items: pool[]
  has capacity: 10
  has gold: 100
  
  on add_item(name) {
    check items.count < capacity: {
      emit items.add(name)
      emit unity("Log", "Added:", name)
    }
    
    check items.count >= capacity: {
      emit unity("Log", "Inventory full!")
    }
  }
  
  on buy_item(name, cost) {
    check gold >= cost: {
      gold - cost -> gold
      emit add_item(name)
    }
  }
}
```

**InventoryUI.cs:**
```cs
using UnityEngine;
using Morphyn.Runtime;

public class InventoryUI : MonoBehaviour
{
    public void AddSword()
    {
        MorphynController.Instance.Emit("Inventory", "add_item", "sword");
    }
    
    public void BuyPotion()
    {
        MorphynController.Instance.Emit("Inventory", "buy_item", "potion", 20);
    }
    
    public void ShowItems()
    {
        MorphynPool items = (MorphynPool)MorphynController.Instance.GetField("Inventory", "items");
        
        foreach (var item in items.Values)
        {
            Debug.Log($"Item: {item}");
        }
    }
}
```

## Enemy Spawner with Timer

**spawner.morphyn:**
```morphyn
entity Spawner {
  has timer: 0
  has interval: 2000
  has maxEnemies: 10
  has currentCount: 0
  
  on tick(dt) {
    timer + dt -> timer
    
    check timer >= interval: {
      check currentCount < maxEnemies: {
        emit spawn
        0 -> timer
      }
    }
  }
  
  on spawn {
    currentCount + 1 -> currentCount
    emit unity("SpawnEnemy")
  }
  
  on enemy_died {
    currentCount - 1 -> currentCount
  }
}
```

**Spawner.cs:**
```cs
using UnityEngine;
using Morphyn.Unity;

public class Spawner : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    
    void Start()
    {
        UnityBridge.Instance.RegisterCallback("SpawnEnemy", args => {
            Instantiate(enemyPrefab, transform.position, Quaternion.identity);
        });
    }
    
    public void OnEnemyKilled()
    {
        MorphynController.Instance.Emit("Spawner", "enemy_died");
    }
}
```