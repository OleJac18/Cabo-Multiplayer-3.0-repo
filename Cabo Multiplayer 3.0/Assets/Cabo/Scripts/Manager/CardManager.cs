using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

// •	Kartenverwaltung: Diese Klasse ist für das Erzeugen, Verteilen und Verwalten von Karten verantwortlich.

public class CardManager : NetworkBehaviour
{
    [CanBeNull] public static event Action<string, RpcParams> OnSendPlayerIds;

    public List<ulong> playerIds = new List<ulong>();
    public List<int> currentPlayerCards = new List<int>();

    [SerializeField] private GameObject _cardprefab;
    [SerializeField] private GameManager _gameManager;

    private void Start()
    {
        _gameManager = FindObjectOfType<GameManager>();
        GameManager.OnUpdateCardsOfCurrentPlayer += UpdateCardsOfCurrentPlayerRpc;
        GameManager.OnSpawnCardServer += SpawnCardServerRpc;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        GameManager.OnUpdateCardsOfCurrentPlayer -= UpdateCardsOfCurrentPlayerRpc;
        GameManager.OnSpawnCardServer -= SpawnCardServerRpc;
    }


    /// <summary>
    /// Diese Methode gibt die Karten des aktuellen Spielers zurück
    /// </summary>
    /// <returns></returns>
    private List<int> GetPlayerCardNumbers()
    {
        List<int> cards = new List<int>();

        Transform playerHandPanel = PlayerManager.instance.GetPlayerHandPanel().transform;

        for (int i = 0; i < playerHandPanel.childCount; i++)
        {
            cards.Add(playerHandPanel.GetChild(i).GetComponent<CardController>().CardNumber);
        }

        return cards;
    }

    //////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// In dieser Methode werden die ersten vier Karten an die Spieler verteilt
    /// </summary>

    [Rpc(SendTo.Server)]
    public void SpawnCardServerRpc()
    {
        Debug.Log("Die Karten werden verteilt");
        foreach (Player player in _gameManager.GetPlayerList())
        {
            Debug.Log("Es wurde ein Spieler mit der ID: "+player.Id + "hinzugefügt.");
            playerIds.Add(player.Id);
        }

        foreach (Player player in _gameManager.GetPlayerList())
        {
            Debug.Log("Es werden Karten an den Spieler mit der ID: " + player.Id + "ausgegeben.");
            for (int i = 0; i < _gameManager.GetHandCards(); i++)
            {
                SpawnCardsRpc(player.Cards[i], RpcTarget.Single(player.Id, RpcTargetUse.Temp));
            }


            // Konvertieren Sie die Liste in einen String, da keine Listen per RPC gesendet werden können
            string playerIdsString = string.Join(",", playerIds);

            // Senden Sie die Liste der Spieler-IDs an den Client
            OnSendPlayerIds?.Invoke(playerIdsString, RpcTarget.Single(player.Id, RpcTargetUse.Temp));
            //SendPlayerIdsClientRpc(playerIdsString, RpcTarget.Single(player.Id, RpcTargetUse.Temp));
        }
    }

    /// <summary>
    /// In dieser Methode werden bei jedem Spieler eine enemyCard und eine playerCard gespawned
    /// </summary>
    /// <param name="cardNumber">Cardnummer, die die Karte haben soll</param>
    /// <param name="rpcParams"></param>
    [Rpc(SendTo.SpecifiedInParams)]
    private void SpawnCardsRpc(int cardNumber, RpcParams rpcParams = default)
    {
        Debug.Log("Eine Karte wird auf dem Client gespawnt");
        GameObject enemyCard = Instantiate(_cardprefab, Vector3.one, Quaternion.identity);
        enemyCard.transform.SetParent(PlayerManager.instance.GetEnemyHandPanel().transform);
        enemyCard.transform.localScale = Vector3.one;
        enemyCard.GetComponent<CardController>().thisCard.correspondingDeck = CardStack.Stack.ENEMYCARD;
        enemyCard.GetComponent<CardController>().SetCanHover(false);
        enemyCard.GetComponent<CardController>().SetIsSelectable(false);

        GameObject playerCard = Instantiate(_cardprefab, Vector3.one, Quaternion.identity);
        playerCard.transform.SetParent(PlayerManager.instance.GetPlayerHandPanel().transform);
        playerCard.transform.localScale = Vector3.one;
        playerCard.GetComponent<CardController>().thisCard.correspondingDeck = CardStack.Stack.PLAYERCARD;
        playerCard.GetComponent<CardController>().SetCanHover(true);
        playerCard.GetComponent<CardController>().SetIsSelectable(true);

        playerCard.GetComponent<CardController>().CardNumber = cardNumber;
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void UpdateCardsOfCurrentPlayerRpc(RpcParams rpcParams = default)
    {
        List<int> cards = GetPlayerCardNumbers();

        // Konvertieren Sie die Liste in einen String, da keine Listen per RPC gesendet werden können
        string cardsString = string.Join(",", cards);

        UpdateCardOfCurrentPlayerServerRPC(NetworkManager.Singleton.LocalClientId, cardsString);
    }

    /// <summary>
    /// Aktualisiert die Karten des aktuellen Spielers in _player
    /// </summary>
    /// <param name="cardsString"></param>
    [Rpc(SendTo.Server)]
    private void UpdateCardOfCurrentPlayerServerRPC(ulong clientId, string cardsString)
    {
        // Konvertieren Sie den String zurück in eine Liste
        currentPlayerCards = new List<int>(Array.ConvertAll(cardsString.Split(','), int.Parse));

        List<Player> _player = _gameManager.GetPlayerList();
        // Suchen Sie den Spieler mit der übergebenen clientId
        Player currentPlayer = _player.FirstOrDefault(p => p.Id == clientId);

        // Überprüfen Sie, ob ein Spieler gefunden wurde
        if (currentPlayer != null)
        {
            // Aktualisieren Sie die Karten des gefundenen Spielers
            currentPlayer.Cards = currentPlayerCards;
        }
        else
        {
            // Behandeln Sie den Fall, wenn kein Spieler gefunden wurde
            Debug.Log("Kein Spieler mit der ClientId " + clientId + " gefunden.");
        }
    }
}
