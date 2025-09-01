// FoodPile.cs
using UnityEngine;
using System;

public class FoodPile : MonoBehaviour
{
    public int amount = 20;

    public Action<FoodPile> OnDepleted;

    public bool Take(int n = 1)
    {
        if (amount <= 0) return false;
        amount -= n;
        if (amount <= 0)
        {
            OnDepleted?.Invoke(this);
            Destroy(gameObject);
        }
        return true;
    }
}
