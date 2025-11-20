using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealthSystem : MonoBehaviour
{
    public float health;
    public float healthMax;
    public float blue;
    public float blueMax;
    public float green;
    public float greenMax;
    public float poise;
    public float poiseMax;
    public float poiseGet;
    public float greenGet;
    public float _time = 0.5f;
    private bool _isGetting = false;


    public IEnumerator Get(float delay)
    {
        _isGetting = true;
        poise = Mathf.Min(poiseMax, poise);
        if (poise < poiseMax)
        {
            poise += poiseGet;
        }
        green = Mathf.Min(greenMax, green);
        if (green < greenMax)
        {
            green += greenGet;
        }
        _isGetting = false;
        yield return new WaitForSeconds(delay);
    }

    private void Update()
    {
        if(!_isGetting)
            StartCoroutine(Get(_time));
    }

    public void TakeDamage(float damage)
    {
        health -= damage;
        health = Mathf.Max(0, health);
    }

    public void TakePoiseDamage(float damage)
    {
        poise -= damage;
        poise = Mathf.Max(0, poise);
    }
}
