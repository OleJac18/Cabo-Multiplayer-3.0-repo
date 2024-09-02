using UnityEngine;

public class TurnManager
{
    private GameManager _gameManager;
    private int _currentPlayerIndex;
    private int _finalRoundInitiatorIndex = -1;

    public TurnManager(GameManager gameManager)
    {
        this._gameManager = gameManager;
        this._currentPlayerIndex = 0;
    }

    public Player GetCurrentPlayer()
    {
        return _gameManager.GetSpecificPlayer(_currentPlayerIndex);
    }

    public int GetCurrentPlayerIndex()
    {
        return _currentPlayerIndex;
    }

    public void AdvanceTurn()
    {
        _currentPlayerIndex = (_currentPlayerIndex + 1) % _gameManager.GetPlayerCount();

        if (_currentPlayerIndex == _finalRoundInitiatorIndex)
        {
            _gameManager.EndGame();
        }
    }


    public void InitiateFinalRound()
    {
        if (_finalRoundInitiatorIndex == -1)
        {
            _finalRoundInitiatorIndex = _currentPlayerIndex;
        }
    }
}