using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CardStack
{
    public enum Stack
    { NONE, PLAYERCARD, ENEMYCARD, CARDDECK, GRAVEYARD }

    public List<Card> stack = new List<Card>();

    public int getStackCount()
    {
        return stack.Count;
    }

    /// <summary> Erstellt ein Kartendeck für Cabo </summary>
    public void CreateDeck(int numberOfCards)
    {
        //Generierung aller Karten (2x0, 4x1-12, 2x13)
        for (int i = 0; i < numberOfCards; i++)
        {
            //Für die 0
            if (i < 2)
            {
                stack.Add(CardDataBase.cardList[0]);
            }

            //Für 1-12
            if (i >= 2 && i < 50)                //2 3 4 5         0/4  = 0 ;  1/4  = 0 ;  2/4  = 0 ;  3/4  = 0 ; 0+1 = 1
            {                                   //                4/4  = 1 ;  5/4  = 1 ;  6/4  = 1 ;  7/4  = 1 ; 1+1 = 2
                int number = ((i - 2) / 4) + 1;           //                ...
                stack.Add(CardDataBase.cardList[number]);              //                44/4 = 11;  45/4 = 11;  46/4 = 11;  47/4 = 11; 11+1= 12
            }

            //Für die 13
            if (i >= 50)
            {
                stack.Add(CardDataBase.cardList[13]);
            }
        }
    }

    ///  <summary> Mischt die Karten </summary>
    public List<Card> ShuffleCards()
    {
        for (int i = 0; i < stack.Count; i++)
        {
            int r = Random.Range(0, stack.Count - 1);
            Card tmp = stack[i];
            stack[i] = stack[r];
            stack[r] = tmp;
        }
        return stack;
    }

    ///  <summary>
    /// Zieht die oberste Karte vom Stapel / Entfernt die oberste Karte von der Liste
    /// </summary>
    /// <returns> top Card of the Stack </returns>
    public int DrawTopCard()
    {
        if (stack.Count == 0)
        {
            Debug.Log("Kann keine Karte vom einem leerem Stapel ziehen");
            return 100;
        }
        else
        {
            // Neue Karte dem Spieler zuweisen und diese vom Kartenstapel entfernen
            int topCardIndex = stack.Count - 1;
            int number = stack[topCardIndex].number;
            stack.RemoveAt(topCardIndex);
            return number;
        }
    }

    ///  <summary>
    /// Zieht eine bestimmte Karte vom Stapel / Entfernt eine Karte von der Liste
    /// </summary>
    /// <returns> Card of the Stack at cardPosition </returns>
    public int DrawCard(int cardPosition)
    {
        if (stack.Count == 0)
        {
            Debug.Log("Keine Karte mehr auf dem Stapel");
            return 100;
        }
        else
        {
            // Neue Karte dem Spieler zuweisen und diese vom Kartenstapel entfernen
            int topCardIndex = stack.Count - 1;
            if (topCardIndex >= cardPosition)
            {
                int number = stack[cardPosition].number;
                stack.RemoveAt(cardPosition);
                return number;
            }
            Debug.Log("Keine Karte an angegebener Stelle");
            return 100;
        }
    }

    /// <summary> Fügt eine Zahl am Ende der Liste hinzu </summary>
    public void AddCardToTheTop(int number)
    {
        stack.Add(CardDataBase.cardList[number]);
    }

    /// <summary> Fügt eine Zahl an vorgegebener Stelle der Liste hinzu </summary>
    public void AddCard(int number, int cardPos)
    {
        stack.Insert(cardPos, CardDataBase.cardList[number]);
    }

    public void PrintList()
    {
        foreach (var item in stack)
        {
            Debug.Log(item);
        }
    }
}