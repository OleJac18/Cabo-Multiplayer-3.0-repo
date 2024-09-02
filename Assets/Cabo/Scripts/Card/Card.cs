[System.Serializable]
public class Card
{
    public int number;
    public CardStack.Stack correspondingDeck;

    public Card()
    {
    }

    public Card(int Number, CardStack.Stack corresDeck)
    {
        number = Number;
        correspondingDeck = corresDeck;
    }
}