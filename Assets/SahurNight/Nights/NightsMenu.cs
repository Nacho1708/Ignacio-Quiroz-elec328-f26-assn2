using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The menus (world-space UI you can point at and click, or poke) and the button panels on the rails.
// Everything here just calls into NightsGame.
public class NightsMenu : MonoBehaviour
{
    public NightsGame game;

    [Header("Menu board")]
    public GameObject board;
    public GameObject mainPanel;
    public Button[] nightButtons;
    public Button customButton;
    public Button jumpscareButton;
    public TMP_Text jumpscareLabel;

    [Header("Custom Night")]
    public GameObject customPanel;
    public TMP_Text[] levelTexts;
    public Button[] minusButtons, plusButtons;
    public Button allZeroButton, allTenButton, allTwentyButton, startCustomButton, backButton;

    [Header("Night card and end screen")]
    public GameObject cardPanel;
    public TMP_Text cardText;
    public GameObject endPanel;
    public TMP_Text endText;
    public Button retryButton, nextButton, menuButton;

    [Header("Rail buttons")]
    public Button[] cameraButtons;
    public Button monitorButton;
    public Button doorButton;
    public TMP_Text doorLabel, panicLabel;

    void Awake()
    {
        for (int i = 0; i < nightButtons.Length; i++)
        {
            int night = i + 1;
            nightButtons[i].onClick.AddListener(() => game.StartNight(night));
        }
        customButton.onClick.AddListener(ShowCustom);
        jumpscareButton.onClick.AddListener(() => game.JumpscaresOn = !game.JumpscaresOn);
        for (int i = 0; i < levelTexts.Length; i++)
        {
            int who = i;
            minusButtons[i].onClick.AddListener(() => game.ChangeCustomLevel(who, -1));
            plusButtons[i].onClick.AddListener(() => game.ChangeCustomLevel(who, +1));
        }
        allZeroButton.onClick.AddListener(() => game.SetAllCustomLevels(0));
        allTenButton.onClick.AddListener(() => game.SetAllCustomLevels(10));
        allTwentyButton.onClick.AddListener(() => game.SetAllCustomLevels(20));
        startCustomButton.onClick.AddListener(() => game.StartNight(6));
        backButton.onClick.AddListener(ShowMain);
        retryButton.onClick.AddListener(game.Retry);
        nextButton.onClick.AddListener(game.NextNight);
        menuButton.onClick.AddListener(game.ShowMenu);

        for (int i = 0; i < cameraButtons.Length; i++)
        {
            int feed = i;
            cameraButtons[i].onClick.AddListener(() => game.SelectCamera(feed));
        }
        monitorButton.onClick.AddListener(game.ToggleMonitor);
        doorButton.onClick.AddListener(game.ToggleDoor);
    }

    public bool CustomOpen => board.activeSelf && customPanel.activeSelf;

    public void ShowMain() => Show(main: true);
    public void ShowCustom() => Show(custom: true);

    public void ShowCard(string text)
    {
        cardText.text = text;
        Show(card: true);
    }

    public void ShowEnd(string text, bool canGoNext)
    {
        endText.text = text;
        nextButton.gameObject.SetActive(canGoNext);
        Show(end: true);
    }

    public void HideBoard() => board.SetActive(false);

    void Show(bool main = false, bool custom = false, bool card = false, bool end = false)
    {
        board.SetActive(true);
        mainPanel.SetActive(main);
        customPanel.SetActive(custom);
        cardPanel.SetActive(card);
        endPanel.SetActive(end);
    }

    // Keeps labels in sync with the game.
    void LateUpdate()
    {
        if (game == null) return;
        if (jumpscareLabel != null) jumpscareLabel.text = game.JumpscaresOn ? "JUMPSCARES: ON" : "JUMPSCARES: OFF";
        for (int i = 0; i < levelTexts.Length && i < game.customLevels.Length; i++)
            levelTexts[i].text = game.customLevels[i].ToString();
        if (doorLabel != null) doorLabel.text = game.DoorClosed ? "DOOR\n<size=60%>CLOSED</size>" : "DOOR\n<size=60%>OPEN</size>";
        if (panicLabel != null) panicLabel.text = game.PanicReady ? "6-7 READY" : "6-7 USED";
    }
}
