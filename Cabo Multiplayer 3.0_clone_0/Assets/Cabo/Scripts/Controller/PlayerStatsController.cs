using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.VisualScripting;

public class PlayerStatsController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _playerNameText;
    [SerializeField] private TextMeshProUGUI _playerScoreText;
    [SerializeField] private TextMeshProUGUI _caboText;
    [SerializeField] private ulong _playerId;

    private Image _image;
    private Color _defaultColor;
    private Color _playerColor;
    private Color _enemyColor;
    private bool _isPlayer;

    // Start is called before the first frame update
    private void Start()
    {

        GameManager.CaboClickedEvent += ShowCaboText;
        GameManager.PlayerTurnChanged += OnPlayerTurnChanged;
        GameManager.OnUpdateScoreUI += UpdatePlayerScore;

        _image = GetComponent<Image>();
        float r = 1f, g = 1f, b = 1f;
        float a = 100f / 255f;
        _defaultColor = new Color(r, g, b, a);

        _playerColor = Color.yellow;
        _enemyColor = Color.yellow;

        _isPlayer = CompareTag("Player1");

        _caboText.enabled = false;
    }

    private void OnDestroy()
    {
        GameManager.CaboClickedEvent -= ShowCaboText;
        GameManager.PlayerTurnChanged -= OnPlayerTurnChanged;
        GameManager.OnUpdateScoreUI -= UpdatePlayerScore;
    }

    public void SetPlayerId(ulong playerId)
    {
        _playerId = playerId;
    }

    public void SetPlayerName(string playerName)
    {
        _playerNameText.text = playerName;
    }

    public void SetPlayerScore(int playerScore)
    {
        _playerScoreText.text = "Score: " + playerScore.ToString();
    }

    private void OnPlayerTurnChanged(bool isPlayerTurn)
    {
        if (_isPlayer)
        {
            ChangePlayerColor(isPlayerTurn ? _playerColor : _defaultColor);
        }
        else
        {
            ChangeEnemyColor(isPlayerTurn ? _defaultColor : _enemyColor);
        }
    }

    private void UpdatePlayerScore(ulong playerId, int score)
    {
        if (playerId == _playerId)
        {
            SetPlayerScore(score);
        }
    }

    private void ChangePlayerColor(Color color)
    {
        _image.color = PlayerManager.instance.GetIsPlayerTurn() ? _playerColor : _defaultColor;
        // Ist dasselbe wie:
        /*if (PlayerManager.instance.GetIsPlayerTurn())
            _image.color = _playerColor;
        else
            _image.color = _defaultColor;*/
    }

    private void ChangeEnemyColor(Color color)
    {
        _image.color = PlayerManager.instance.GetIsPlayerTurn() ? _defaultColor : _enemyColor;
        // Ist dasselbe wie:
        /*if (!PlayerManager.instance.GetIsPlayerTurn())
            _image.color = _enemyColor;
        else
            _image.color = _defaultColor;*/
    }

    // Ändern Sie die Methode, um die Spieler-ID zu überprüfen
    private void ShowCaboText(ulong playerId, bool show)
    {
        if (playerId == _playerId)
        {
            _caboText.enabled = show;
        }
    }
}