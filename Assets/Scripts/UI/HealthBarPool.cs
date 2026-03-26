using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple pool for EnemyWorldHealthBar prefabs
/// </summary>
public class HealthBarPool : MonoBehaviour
{
    public EnemyWorldHealthBar prefab;
    public int initialSize = 10;

    private Queue<EnemyWorldHealthBar> pool = new Queue<EnemyWorldHealthBar>();

    private void Awake()
    {
        for (int i = 0; i < initialSize; i++)
        {
            var inst = Instantiate(prefab, transform);
            inst.gameObject.SetActive(false);
            pool.Enqueue(inst);
        }
    }

    public EnemyWorldHealthBar Get()
    {
        EnemyWorldHealthBar item;
        if (pool.Count > 0)
            item = pool.Dequeue();
        else
            item = Instantiate(prefab, transform);
        item.gameObject.SetActive(true);
        return item;
    }

    public void Return(EnemyWorldHealthBar item)
    {
        item.gameObject.SetActive(false);
        item.Unbind();
        item.followTarget = null;
        pool.Enqueue(item);
    }
}
