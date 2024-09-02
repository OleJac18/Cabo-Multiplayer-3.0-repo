using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Player
{
    [SerializeField] private ulong id;
    [SerializeField] private List<int> cards;
    [SerializeField] private string name;
    [SerializeField] private int score;
    [SerializeField] private bool hasCalledCabo;

    public Player()
    {
    }

    public Player(ulong Id, List<int> Cards, string Name, int Score)
    {
        id = Id;
        cards = Cards;
        name = Name;
        score = Score;
    }

    public ulong Id
    {
        get { return id; }
        set { id = value; }
    }

    public List<int> Cards
    {
        get { return cards; }
        set { cards = value; }
    }

    public string Name
    {
        get { return name; }
        set { name = value; }
    }

    public int Score
    {
        get { return score; }
        set { score = value; }
    }

    public bool HasCalledCabo
    {
        get { return hasCalledCabo; }
        set { hasCalledCabo = value; }
    }
}
