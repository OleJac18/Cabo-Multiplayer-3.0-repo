using System.Collections.Generic;
using UnityEngine;

public class CardDataBase : MonoBehaviour
{
    public static List<Card> cardList = new List<Card>();

    private void Awake()
    {
        for (int i = 0; i <= 13; i++)
        {
            cardList.Add(new Card(i, CardStack.Stack.NONE));
        }
    }
}