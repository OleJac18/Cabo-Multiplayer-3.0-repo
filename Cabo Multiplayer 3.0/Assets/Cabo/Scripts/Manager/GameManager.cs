using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Ein GameManager ist in der Regel dafür verantwortlich, den allgemeinen Spielzustand zu verwalten. Dies kann Folgendes umfassen:
// •	Verwaltung des Spielzustands (z.B. Start, Pause, Ende)
// •	Verwaltung von Spielereignissen (z.B. Spielstart, Spielende)
// •	Verwaltung von Spielern und deren Zuständen
// •	Verwaltung von Leveln oder Szenen
// •	Verwaltung von Spielregeln und Spiellogik

public class GameManager : NetworkBehaviour
{
    [SerializeField] private List<Player> _player = new List<Player>();

    public static int playerCount = 2;

    [CanBeNull] public static event Action GraveyardSpawnFirstCardEvent;

    [CanBeNull] public static event Action<bool> ResetHasRunOnHostEvent;

    [CanBeNull] public static event Action<ulong, bool> CaboClickedEvent;

    [CanBeNull] public static event Action<bool> PlayerTurnChanged;

    [CanBeNull] public static event Action<ulong, int> OnUpdateScoreUI;

    [CanBeNull] public static event Action<RpcParams> OnUpdateCardsOfCurrentPlayer;

    [CanBeNull] public static event Action OnSpawnCardServer;

    private const int _handCards = 4;
    private TurnManager _turnManager;
    [SerializeField] private static bool _firstRound = true;
    public bool firstRound = true;

    private void Awake()
    {
        if (!_firstRound)
        {
            Destroy(this.gameObject);
        }
    }

    // Start is called before the first frame update
    private void Start()
    {
        if (_firstRound)
        {
            DontDestroyOnLoad(this.gameObject);
        }

        this._turnManager = new TurnManager(this);

        NetworkManager.Singleton.OnConnectionEvent += SavePlayerId;
        NetworkManager.Singleton.OnConnectionEvent += CheckAmountOfConnectedClients;
        Graveyard.CardReachedGraveyardEvent += NextTurn;
        PlayerManager.CardReachedPlayerHandEvent += NextTurn;

        //if (!IsServer) return;
        if (IsServer && _firstRound)
        {
            Debug.Log("Ich bin in der ersten Runde");
            // Überprüfen Sie, ob es bereits verbundene Clients gibt
            foreach (var client in NetworkManager.Singleton.ConnectedClients)
            {
                Debug.Log("Ein Client mit der ClientId " + client.Key + " ist bereits verbunden");
                // Erstellen Sie ein ConnectionEventData-Objekt für den verbundenen Client
                ConnectionEventData connectionEventData = new ConnectionEventData();
                connectionEventData.EventType = ConnectionEvent.ClientConnected;
                connectionEventData.ClientId = client.Key;

                // Rufen Sie SavePlayerId manuell für den verbundenen Client auf
                SavePlayerId(NetworkManager.Singleton, connectionEventData);
            }
            _firstRound = false;
        }
        else
        {
            _firstRound = false;
        }
    }
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Fügen Sie hier die Logik ein, die Sie ausführen möchten, wenn eine Szene geladen wird
        Debug.Log("Szene geladen: " + scene.name);
        if (IsServer && !_firstRound)
        {
            Debug.Log("Ich bin in der zweiten oder einer späteren Runde");
            _turnManager = new TurnManager(this);
            StartCoroutine(WaitToServFirstCards(NetworkManager.Singleton));
        }
    }


    private void Update()
    {
        firstRound = _firstRound;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        NetworkManager.Singleton.OnConnectionEvent -= SavePlayerId;
        NetworkManager.Singleton.OnConnectionEvent -= CheckAmountOfConnectedClients;
        Graveyard.CardReachedGraveyardEvent -= NextTurn;
        PlayerManager.CardReachedPlayerHandEvent -= NextTurn;
    }

    public List<Player> GetPlayerList()
    {
        return _player;
    }

    /// <summary>
    /// Gibt den Spieler anhand des Index zurück
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public Player GetSpecificPlayer(int index)
    {
        return _player[index];
    }

    /// <summary>
    /// Gibt die Anzahl der Spieler zurück
    /// </summary>
    /// <returns></returns>
    public int GetPlayerCount()
    {
        return _player.Count;
    }

    public int GetHandCards()
    {
        return _handCards;
    }

    /// <summary>
    /// Diese Methode wird von den Clients aufgerufen, wenn ein neuer Zug beginnt
    /// </summary>
    public void NextTurn(ulong clientId)
    {
        NextTurnServerRpc(clientId);
    }

    /// <summary>
    /// Coroutine, die den nächsten Zug nach einer Sekunde startet, damit sich die Karten auf der Client-Seite aktualisieren können
    /// Ansonsten wird noch eine 13 in der Hand angezeigt, obwohl diese nur als Platzhalter gilt für die neue Handkarte. 
    /// </summary>
    /// <returns></returns>
    IEnumerator NextTurnCoroutine(ulong clientId)
    {
        yield return new WaitForSeconds(1f);
        OnUpdateCardsOfCurrentPlayer?.Invoke(RpcTarget.Single(clientId, RpcTargetUse.Temp));

        Debug.Log("----------- Der nächste Zug beginnt -----------");
        ChangeCurrentPlayer();
    }

    /// <summary>
    /// Die Methode ändert den Spieler, der am Zug ist
    /// </summary>
    private void ChangeCurrentPlayer()
    {
        _turnManager.AdvanceTurn();

        Player currentPlayer = _turnManager.GetCurrentPlayer();
        foreach (Player player in _player)
        {
            if (player.Id == currentPlayer.Id)
            {
                SetCurrentPlayerClientRpc(true, RpcTarget.Single(player.Id, RpcTargetUse.Temp));
            }
            else
            {
                SetCurrentPlayerClientRpc(false, RpcTarget.Single(player.Id, RpcTargetUse.Temp));
            }
        }
    }

    /// <summary>
    /// Legt einen neuen Spieler an, wenn sie ein neuer Client mit dem Server verbindet
    /// </summary>
    /// <param name="networkManager"></param>
    /// <param name="connectionEventData"></param>
    public void SavePlayerId(NetworkManager networkManager, ConnectionEventData connectionEventData)
    {
        if (!IsServer) return;

        if (connectionEventData.EventType == ConnectionEvent.ClientConnected)
        {
            Debug.Log("Ein Client mit der ClientId " + connectionEventData.ClientId + " ist beigetreten");
            _player.Add(new Player(connectionEventData.ClientId, new List<int>(), "Player " + connectionEventData.ClientId, 0));
        }
    }

    /// <summary>
    /// Checkt, ob sich ein Client verbunden hat und gibt die ersten Karten aus, wenn alle Spieler verbunden sind
    /// </summary>
    /// <param name="networkManager"></param>
    /// <param name="connectionEventData"></param>
    public void CheckAmountOfConnectedClients(NetworkManager networkManager, ConnectionEventData connectionEventData)
    {
        if (!IsServer) return;
        Debug.Log("CheckAmountOfConnectedClients");

        if (connectionEventData.EventType == ConnectionEvent.ClientConnected)
        {
            int clientCount = networkManager.ConnectedClients.Count;
            if (clientCount < playerCount) return;

            //ServFirstCards(networkManager);

            StartCoroutine(WaitToServFirstCards(networkManager));
        }
    }

    IEnumerator WaitToServFirstCards(NetworkManager networkManager)
    {
        yield return new WaitForSeconds(1f);
        ServFirstCards(networkManager);
        UpdateScoreUI();
    }

    /// <summary>
    /// Zieht die ersten vier Karten für jeden Spieler und lässt diese bei jedem Spieler spawned
    /// </summary>
    /// <param name="networkManager"></param>
    private void ServFirstCards(NetworkManager networkManager)
    {
        Debug.Log("Die ersten Karten werden ausgegeben");
        for (int client = 0; client < networkManager.ConnectedClients.Count; client++)
        {
            _player[client].HasCalledCabo = false;
            _player[client].Cards.Clear();
            for (int i = 0; i < _handCards; i++)
            {
                _player[client].Cards.Add(CardDeck.instance.DrawTopCard());
            }
        }

        GraveyardSpawnFirstCardEvent?.Invoke();
        OnSpawnCardServer?.Invoke();

        Player currentPlayer = _turnManager.GetCurrentPlayer();
        Debug.Log("Der aktuelle Spieler ist " + currentPlayer.Id);

        // Setzt bei allen Clients den aktuellen Spieler
        foreach (Player player in _player)
        {
            if (player.Id == currentPlayer.Id)
            {
                SetCurrentPlayerClientRpc(true, RpcTarget.Single(player.Id, RpcTargetUse.Temp));
            }
            else
            {
                SetCurrentPlayerClientRpc(false, RpcTarget.Single(player.Id, RpcTargetUse.Temp));
            }
        }
    }

    /// <summary>
    /// Diese Methode wird aufgerufen, wenn der Cabo-Button geklickt wird
    /// </summary>
    public void HandleCaboClicked()
    {
        ExecuteCaboActionsServerRPC(NetworkManager.Singleton.LocalClientId);
    }

    /// <summary>
    /// Diese Methode berechnet und aktualisiert den Score eines jeden Spielers
    /// </summary>
    public void CalculatePlayerScores()
    {
        // Finden Sie den Spieler mit der niedrigsten Punktzahl
        int lowestScore = _player.Min(p => p.Cards.Sum());
        List<Player> playersWithLowestScore = _player.Where(p => p.Cards.Sum() == lowestScore).ToList();

        // Überprüfen Sie, ob einer der Spieler mit der niedrigsten Punktzahl "Cabo" gerufen hat
        Player playerWhoCalledCabo = playersWithLowestScore.FirstOrDefault(p => p.HasCalledCabo);

        // Wenn ein Spieler "Cabo" gerufen hat, erhält nur dieser Spieler keine zusätzlichen Punkte
        if (playerWhoCalledCabo != null)
        {
            foreach (Player player in _player)
            {
                if (player != playerWhoCalledCabo)
                {
                    player.Score += player.Cards.Sum();
                }
            }
        }
        // Wenn kein Spieler "Cabo" gerufen hat, erhalten alle Spieler mit der niedrigsten Punktzahl keine zusätzlichen Punkte
        else
        {
            foreach (Player player in _player)
            {
                if (!playersWithLowestScore.Contains(player))
                {
                    player.Score += player.Cards.Sum();
                }
            }
        }
    }


    /// <summary>
    /// Diese Methode wird aufgerufen, wenn das Spiel zu Ende ist
    /// </summary>
    public void EndGame()
    {
        CalculatePlayerScores();
        UpdateScoreUI();

        Debug.Log("----------- DIE RUNDE IST ZU ENDE -----------");
        SavePlayer();
        StartCoroutine(Restart());
    }

    private void UpdateScoreUI()
    {
        foreach (Player player in _player)
        {
            UpdateScoreUIRPC(player.Id, player.Score);
        }
    }

    private IEnumerator Restart()
    {
        yield return new WaitForSeconds(5f);

        // Wechseln Sie die Szene auf dem Server und allen Clients
        NetworkManager.SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
    }

    private void SavePlayer()
    {
        SaveSystem.SavePlayer(_player[0]);
    }

    private void LoadPlayer()
    {
        Player player = SaveSystem.LoadPlayer();
        _player.Add(player);
        Debug.Log("Loaded player name: " + player.Name);
    }

    //////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Diese Methode wird aufgerufen, wenn der Spieler, der am Zug ist, geändert wird
    /// </summary>
    /// <param name="isPlayerTurn"></param>
    /// <param name="rpcParams"></param>
    [Rpc(SendTo.SpecifiedInParams)]
    private void SetCurrentPlayerClientRpc(bool isPlayerTurn, RpcParams rpcParams = default)
    {
        PlayerManager.instance.SetIsPlayerTurn(isPlayerTurn);
        ResetHasRunOnHostEvent?.Invoke(false);
        PlayerTurnChanged?.Invoke(isPlayerTurn);
    }

    /// <summary>
    /// Diese Methode wird auf dem Server aufgerufen, wenn ein neuer Zug beginnt
    /// </summary>
    [Rpc(SendTo.Server)]
    public void NextTurnServerRpc(ulong clientId)
    {
        StartCoroutine(NextTurnCoroutine(clientId));
    }
    /// <summary>
    /// Diese Methode wird aufgerufen, wenn der Cabo-Button geklickt wird
    /// </summary>
    [Rpc(SendTo.Server)]
    private void ExecuteCaboActionsServerRPC(ulong clientId)
    {
        Debug.Log("Cabo wurde geklickt");

        // Starten Sie die letzte Runde
        _turnManager.InitiateFinalRound();

        // Bestimmt , welcher Spieler "Cabo" gerufen hat
        Player currentPlayer = _turnManager.GetCurrentPlayer();
        currentPlayer.HasCalledCabo = true;
        CaboClickedEventRpc(currentPlayer.Id);

        // Beendet den aktuellen Zug
        NextTurn(clientId);
    }

    /// <summary>
    /// Durch diese Methode wird bei allen Clients aktualisiert, welcher Spieler Cabo geklickt hat
    /// </summary>
    /// <param name="currentPlayerId"></param>
    [Rpc(SendTo.Everyone)]
    private void CaboClickedEventRpc(ulong currentPlayerId)
    {
        CaboClickedEvent?.Invoke(currentPlayerId, true);
    }

    /// <summary>
    /// Durch diese Methode wird bei allen Clients die UI aktualisiert, um den Score des Spielers anzuzeigen
    /// </summary>
    /// <param name="clientId"></param>
    /// <param name="score"></param>
    [Rpc(SendTo.Everyone)]
    private void UpdateScoreUIRPC(ulong clientId, int score)
    {
        OnUpdateScoreUI?.Invoke(clientId, score);
    }

}