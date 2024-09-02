using JetBrains.Annotations;
using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardDeck : NetworkBehaviour, IPointerClickHandler
{
    public static CardDeck instance; // Ist für größere Projekte nicht angemessen

    private const int CardSize = 52;

    [SerializeField] private CardStack _cardstack;

    private bool _hasRunOnHost = false;

    private GameObject _spawnedCard;

    private CardController _spawnedCardController;

    private GameObject _topCard;

    [SerializeField] private int _topCardNumber = -1;

    [SerializeField] private GameObject spawnCardPrefab;

    [SerializeField] private GameObject spawnEnemyPos;

    [SerializeField] private GameObject spawnPlayerPos;

    [CanBeNull] public static event Action CardDeckClickedEvent;

    // Start is called before the first frame update
    private void Start()
    {
        instance = this;
        _topCard = this.transform.GetChild(0).gameObject;
        GameManager.ResetHasRunOnHostEvent += SetHasRunOnHost;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        GameManager.ResetHasRunOnHostEvent -= SetHasRunOnHost;
    }

    public void SetHasRunOnHost(bool hasExecutedOnHost)
    {
        _hasRunOnHost = hasExecutedOnHost;
    }

    /// <summary>
    /// Wenn das erste Mal eine Karte von CardDeck gezogen wird, heißt es, dass mindestens 2 Karten auf dem Graveyard liegen
    /// Durch dieses Event wird eine Methode in Graveyard getriggert, die die Variable "thereIsASecondCard" auf true setzt
    /// </summary>
    [Rpc(SendTo.Everyone)]
    public void CardDeckClickedEventRpc()
    {
        CardDeckClickedEvent?.Invoke();
    }

    // Wirft die oberste Karte vom Kartenstapel ab
    public void DiscardCard()
    {
        _cardstack.DrawTopCard();
    }

    /// <summary>
    /// Wirft die oberste Karte vom Kartenstapel ab
    /// </summary>
    [Rpc(SendTo.Server)]
    public void DiscardCardServerRpc()
    {
        _cardstack.DrawTopCard();
    }

    // Methode, um außerhalb des Kartendecks die oberste Karte zu ziehen
    public int DrawTopCard()
    {
        return _cardstack.DrawTopCard();
    }

    public CardStack GetcardStack()
    {
        return _cardstack;
    }

    /// <summary>
    /// Holt sich die oberste Karte vom CardDeck. Da aber nur der Server das Kartendeck hat, muss ein Rpc Call
    /// zum Server gemacht werden. Danach wird die oberste Karte vom CardDeck bei dem spezifischen Client
    /// gespawnt, bei dem auf das CardDeck geklickt wurde.
    /// </summary>
    /// <param name="clientId"></param>
    [Rpc(SendTo.Server)]
    public void GetTopCardDeckCardServerRpc(ulong clientId)
    {
        if (!IsThereNoSpawnedCard()) return;

        CardDeckClickedEventRpc();

        _topCardNumber = CardDeck.instance.DrawTopCard();
        SpawnCardDeckCardSpecificClientRpc(_topCardNumber, RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }

    public bool IsThereNoSpawnedCard()
    {
        //Überprüft ob es bereits eine gespawnte Karte von einem der Kartenstapel gibt
        if (_spawnedCard == null)
        {
            _spawnedCard = GameObject.FindGameObjectWithTag("SpawnDeck");
        }

        // Spawned eine neue Karte, wenn es nicht bereits schon eine gibt. Diese ist die oberste vom Kartenstapel
        // Diese bewegt sich vom Kartenstapel auf eine definierte Position beim Spieler
        // Nachdem sie bei der Position angekommen ist, wird sie umgedreht
        if (_spawnedCard == null)
        {
            return true;
        }
        else
        {
            Debug.Log("Es wurde bereits eine Karte gezogen");
            return false;
        }
    }

    // Start is called before the first frame update
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            _cardstack = new CardStack();
            _cardstack.CreateDeck(CardSize);
            _cardstack.ShuffleCards();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Wenn es einen reinen Server gibt, wird auf dem Server keine Interaktion ausgeführt
        if (IsServer && !IsHost) return;

        // Überprüfen, ob der aktuelle Spieler am Zug ist
        if (!PlayerManager.instance.GetIsPlayerTurn()) return;

        Debug.Log("Carddeck geklickt");

        if (!IsServer && !IsHost)
        {
            GetTopCardDeckCardServerRpc(NetworkManager.LocalClientId);
        }
        else
        {
            _topCardNumber = _cardstack.stack[_cardstack.getStackCount() - 1].number;
            bool spawnACard = IsThereNoSpawnedCard();
            if (spawnACard)
            {
                CardDeckClickedEventRpc();

                SpawnCard(_topCardNumber);
                LeanTween.move(_spawnedCard, spawnPlayerPos.transform.position, 0.5f).setOnComplete(FlipCard);
                DiscardCard();
                SpawnCardDeckCardClientRpc();
            }
        }
    }

    public void SpawnCard(int topCardNumber)
    {
        // Spawned eine neue Karte, wenn es nicht bereits schon eine gibt. Diese ist die oberste vom Kartenstapel
        // Diese bewegt sich vom Kartenstapel auf eine definierte Position beim Spieler
        // Nachdem sie bei der Position angekommen ist, wird sie umgedreht
        _spawnedCard = Instantiate(spawnCardPrefab, _topCard.transform.position, _topCard.transform.rotation);
        _spawnedCardController = _spawnedCard.GetComponent<CardController>();
        _spawnedCard.transform.SetParent(this.transform);
        _spawnedCard.transform.tag = "SpawnDeck";
        _spawnedCardController.thisCard.correspondingDeck = CardStack.Stack.CARDDECK;
        _spawnedCard.transform.localScale = new Vector3(1, 1, 1);
        _spawnedCardController.SetCardBackImageVisibility(true);
        _spawnedCardController.CardNumber = topCardNumber;
    }

    /// <summary>
    /// Spawned eine CardDeck Karte bei allen Clients/Server, die nicht auf die CardDeck Karte geklickt haben
    /// </summary>
    [Rpc(SendTo.NotMe)]
    public void SpawnCardDeckCardClientRpc()
    {
        // Wenn es einen reinen Server gibt, dann wird auf diesem keine Karte gespawned
        if (IsServer && !IsHost) return;

        if (ShouldReturnOnServerOrHost()) return;

        Debug.Log("Client Spawned a CardDeck Card!");

        bool spawnACard = IsThereNoSpawnedCard();
        if (spawnACard)
        {
            SpawnCard(-1);
            LeanTween.move(_spawnedCard, spawnEnemyPos.transform.position, 0.5f).setOnComplete(() =>
            {
                _hasRunOnHost = false;
            }); ;
        }
    }

    ////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Spawned die oberste Karte vom CardDeck bei dem spezifischen Client  bei dem auf das CardDeck geklickt wurde
    /// Im Anschluss wird bei allen anderen Clients auch die oberste Karte gespawnt aber mit einer -1 als Kartennummer
    /// </summary>
    /// <param name="cardNumber"></param>
    /// <param name="rpcParams"></param>
    [Rpc(SendTo.SpecifiedInParams)]
    public void SpawnCardDeckCardSpecificClientRpc(int cardNumber, RpcParams rpcParams = default)
    {
        Debug.Log("topCardNumber from ClientRpc Call: " + cardNumber);
        _topCardNumber = cardNumber;
        bool spawnACard = IsThereNoSpawnedCard();
        if (spawnACard)
        {
            SpawnCard(_topCardNumber);
            LeanTween.move(_spawnedCard, spawnPlayerPos.transform.position, 0.5f).setOnComplete(FlipCard);
            DiscardCardServerRpc();
            SpawnCardDeckCardClientRpc();
        }
    }

    // Dreht die Karte um
    private void FlipCard()
    {
        LeanTween.rotateY(_spawnedCard, 90.0f, 0.5f).setOnComplete(() =>
        {
            _spawnedCardController.SetCardBackImageVisibility(false);
            LeanTween.rotateY(_spawnedCard, 0.0f, 0.5f);
        });
    }

    private bool ShouldReturnOnServerOrHost()
    {
        // Wenn der Code auf dem Host läuft und bereits einmal ausgeführt wurde, brechen wir ab
        if (IsHost && _hasRunOnHost) return true;

        // Wenn der Code auf dem Host läuft und noch nicht ausgeführt wurde, setzen wir hasRunOnHost auf true
        if (IsHost && !_hasRunOnHost) _hasRunOnHost = true;

        return false;
    }
}