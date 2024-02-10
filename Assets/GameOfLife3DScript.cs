using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using Rnd = UnityEngine.Random;

public class GameOfLife3DScript : MonoBehaviour
{

    static int _moduleIdCounter = 1;
    int _moduleID = 0;

    public KMBombModule Module;
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMSelectable[] Cells;
    public KMSelectable[] Arrows;
    public KMSelectable[] Buttons;

    private List<bool> Answer, InitialBoard, InputBoard;
    private static readonly int[][] AdjacentCells = new int[][]
    {
        new int[] { 1, 3, 4, 0 + 9, 1 + 9, 3 + 9 },
        new int[] { 0, 2, 3, 4, 5, 0 + 9, 1 + 9, 2 + 9 },
        new int[] { 1, 4, 5, 1 + 9, 2 + 9, 4 + 9 },
        new int[] { 0, 1, 4, 6, 7, 0 + 9, 3 + 9, 5 + 9 },
        new int[] { 0, 1, 2, 3, 5, 6, 7, 8 },
        new int[] { 1, 2, 4, 7, 8, 2 + 9, 4 + 9, 7 + 9 },
        new int[] { 3, 4, 7, 3 + 9, 5 + 9, 6 + 9 },
        new int[] { 3, 4, 5, 6, 8, 5 + 9, 6 + 9, 7 + 9 },
        new int[] { 4, 5, 7, 4 + 9, 6 + 9, 7 + 9 },

        new int[] { 0, 1, 3, 1 + 9, 3 + 9, 0 + 17, 1 + 17, 3 + 17 },
        new int[] { 0, 1, 2, 0 + 9, 2 + 9, 0 + 17, 1 + 17, 2 + 17 },
        new int[] { 1, 2, 5, 1 + 9, 4 + 9, 1 + 17, 2 + 17, 5 + 17 },
        new int[] { 0, 3, 6, 0 + 9, 5 + 9, 0 + 17, 3 + 17, 6 + 17 },
        //This is where the centre cell would have been, if there was one. :)
        new int[] { 2, 5, 8, 2 + 9, 7 + 9, 2 + 17, 5 + 17, 8 + 17 },
        new int[] { 3, 6, 7, 3 + 9, 6 + 9, 3 + 17, 6 + 17, 7 + 17 },
        new int[] { 6, 7, 8, 5 + 9, 7 + 9, 6 + 17, 7 + 17, 8 + 17 },
        new int[] { 5, 7, 8, 4 + 9, 6 + 9, 5 + 17, 7 + 17, 8 + 17 },

        new int[] { 0 + 9, 1 + 9, 3 + 9, 1 + 17, 3 + 17, 4 + 17 },
        new int[] { 0 + 9, 1 + 9, 2 + 9, 0 + 17, 2 + 17, 3 + 17, 4 + 17, 5 + 17 },
        new int[] { 1 + 9, 2 + 9, 4 + 9, 1 + 17, 4 + 17, 5 + 17 },
        new int[] { 0 + 9, 3 + 9, 5 + 9, 0 + 17, 1 + 17, 4 + 17, 6 + 17, 7 + 17 },
        new int[] { 0 + 17, 1 + 17, 2 + 17, 3 + 17, 5 + 17, 6 + 17, 7 + 17, 8 + 17 },
        new int[] { 2 + 9, 4 + 9, 7 + 9, 1 + 17, 2 + 17, 4 + 17, 7 + 17, 8 + 17 },
        new int[] { 3 + 9, 5 + 9, 6 + 9, 3 + 17, 4 + 17, 7 + 17 },
        new int[] { 5 + 9, 6 + 9, 7 + 9, 3 + 17, 4 + 17, 5 + 17, 6 + 17, 8 + 17 },
        new int[] { 4 + 9, 6 + 9, 7 + 9, 4 + 17, 5 + 17, 7 + 17 }
    };
    private Coroutine[] CellAnimCoroutines, ArrowAnimCoroutines, ButtonAnimCoroutines;
    private List<Vector3> InitCellPositions = new List<Vector3>();
    private Color[] CellColours = new Color[] { new Color(32f / 255f, 32f / 255f, 32f / 255f), Color.white };
    private int XRotation, ZRotation;
    private bool CannotPress = true, Solved;

    private bool Iterate(int pos)
    {
        var count = AdjacentCells[pos].Select(x => InitialBoard[x] ? 1 : 0).Sum();
        return count == 3 || (InitialBoard[pos] && count == 2);
    }

    void Awake()
    {
        _moduleID = _moduleIdCounter++;
        for (int i = 0; i < Cells.Length; i++)
        {
            int x = i;
            Cells[x].OnInteract += delegate { if (!CannotPress) CubePress(x); return false; };
            InitCellPositions.Add(Cells[x].transform.localPosition);
        }
        for (int i = 0; i < Arrows.Length; i++)
        {
            int x = i;
            Arrows[x].OnInteract += delegate { if (!CannotPress) ArrowPress(x); return false; };
        }
        for (int i = 0; i < Buttons.Length; i++)
        {
            int x = i;
            Buttons[x].OnInteract += delegate { if (!CannotPress) ButtonPress(x); return false; };
        }
        CellAnimCoroutines = new Coroutine[Cells.Length];
        ArrowAnimCoroutines = new Coroutine[Arrows.Length];
        ButtonAnimCoroutines = new Coroutine[Buttons.Length];
        for (int i = 0; i < Cells.Length; i++)
            Cells[i].GetComponent<MeshRenderer>().material.color = CellColours[0];
        Module.OnActivate += delegate { Initialise(); UpdateBoard(); CannotPress = false; };
        StartCoroutine(Wiggle());
    }

    void Start()
    {
        SetLettersActive(TwitchPlaysActive);
    }

    void SetLettersActive(bool status)
    {
        var letters = Cells[0].transform.parent.GetComponentsInChildren<TextMesh>(true);
        foreach (var letter in letters)
            letter.gameObject.SetActive(status);
    }

    void Initialise()
    {
        InitialBoard = new List<bool>();
        for (int i = 0; i < Cells.Length; i++)
            InitialBoard.Add(Rnd.Range(0, 2) == 0);
        InputBoard = InitialBoard.ToList();
        Debug.LogFormat("[Game of Life 3D #{0}] The grid is as follows, starting with the front-top-left cell and moving in reading order, looking perpendicular to the module (0 = dead, 1 = alive): {1}.", _moduleID, InitialBoard.Select(x => x ? "1" : "0").Join(", "));
        Answer = new List<bool>();
        for (int i = 0; i < Cells.Length; i++)
            Answer.Add(Iterate(i));
        Debug.LogFormat("[Game of Life 3D #{0}] The answer is as follows, read the same way: {1}.", _moduleID, Answer.Select(x => x ? "1" : "0").Join(", "));
    }

    void UpdateBoard()
    {
        for (int i = 0; i < Cells.Length; i++)
            Cells[i].GetComponent<MeshRenderer>().material.color = InputBoard[i] ? CellColours[1] : CellColours[0];
    }

    void CubePress(int pos)
    {
        if (CellAnimCoroutines[pos] != null)
            StopCoroutine(CellAnimCoroutines[pos]);
        CellAnimCoroutines[pos] = StartCoroutine(CellPressAnim(pos));
        InputBoard[pos] = !InputBoard[pos];
        Audio.PlaySoundAtTransform("press " + (InputBoard[pos] ? "white" : "black"), Cells[pos].transform);
        Cells[pos].AddInteractionPunch(0.5f);
        UpdateBoard();
    }

    void ArrowPress(int pos)
    {
        Audio.PlaySoundAtTransform("arrow press", Arrows[pos].transform);
        Cells[pos].AddInteractionPunch(0.5f);
        if (ArrowAnimCoroutines[pos] != null)
            StopCoroutine(ArrowAnimCoroutines[pos]);
        ArrowAnimCoroutines[pos] = StartCoroutine(SelectablePressAnim(Arrows[pos].transform, 0.05f, 0, -0.001f));
        StartCoroutine(RotateCube(pos));
    }

    void ButtonPress(int pos)
    {
        Audio.PlaySoundAtTransform("button press", Buttons[pos].transform);
        Cells[pos].AddInteractionPunch();
        if (ButtonAnimCoroutines[pos] != null)
            StopCoroutine(ButtonAnimCoroutines[pos]);
        ButtonAnimCoroutines[pos] = StartCoroutine(SelectablePressAnim(Buttons[pos].transform, 0.075f, -0.0039f, -0.0069f));
        switch (pos)
        {
            case 0:
                StartCoroutine(ResetCube());
                break;
            case 1:
                StartCoroutine(ClearCube());
                break;
            default:
                var correct = true;
                for (int i = 0; i < InputBoard.Count(); i++)
                    if (Answer[i] != InputBoard[i])
                        correct = false;
                if (correct)
                    StartCoroutine(Solve());
                else
                {
                    Module.HandleStrike();
                    Debug.LogFormat("[Game of Life 3D #{0}] You submitted an incorrect answer: {1}. Strike!", _moduleID, InputBoard.Select(x => x ? "1" : "0").Join(", "));
                }
                break;
        }
    }

    private IEnumerator CellPressAnim(int pos, float duration = 0.075f, float depression = 0.95f)
    {
        Cells[pos].transform.localPosition = InitCellPositions[pos];
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            Cells[pos].transform.localPosition = Vector3.Lerp(InitCellPositions[pos], InitCellPositions[pos] * depression, timer / duration);
        }
        Cells[pos].transform.localPosition = InitCellPositions[pos] * depression;
        timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            Cells[pos].transform.localPosition = Vector3.Lerp(InitCellPositions[pos] * depression, InitCellPositions[pos], timer / duration);
        }
        Cells[pos].transform.localPosition = InitCellPositions[pos];
    }

    private IEnumerator SelectablePressAnim(Transform target, float duration, float from, float to)
    {
        target.localPosition = new Vector3(target.transform.localPosition.x, from, target.transform.localPosition.z);
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            target.localPosition = new Vector3(target.transform.localPosition.x, Mathf.Lerp(from, to, timer / duration), target.transform.localPosition.z);
        }
        target.localPosition = new Vector3(target.transform.localPosition.x, to, target.transform.localPosition.z);
        timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            target.localPosition = new Vector3(target.transform.localPosition.x, Mathf.Lerp(to, from, timer / duration), target.transform.localPosition.z);
        }
        target.localPosition = new Vector3(target.transform.localPosition.x, from, target.transform.localPosition.z);
    }

    private IEnumerator Wiggle(float speed1 = 2f, float speed2 = 1f, float speed3 = Mathf.PI * 2f / 3f, float maxAngle = 3f, float variance = 0.5f)
    {
        var target = Cells[0].transform.parent.parent;
        speed1 += Rnd.Range(-variance, variance);
        speed2 += Rnd.Range(-variance / 2, variance / 2);
        speed3 += Rnd.Range(-variance, variance);
        while (true)
        {
            target.transform.localEulerAngles = new Vector3(Mathf.Sin((speed1 / 4) * Time.time) * maxAngle, Mathf.Sin((speed2 / 4) * Time.time) * maxAngle, Mathf.Sin((speed3 / 4) * Time.time) * maxAngle);
            yield return null;
        }
    }

    private IEnumerator RotateCube(int pos, float duration = 0.15f)
    {
        CannotPress = true;
        var lastRotation = Cells[0].transform.parent.localRotation;
        var determinedRotation = new[] { Vector3.right, Vector3.back, Vector3.left, Vector3.forward }[pos] * 90;
        var nextRotation = Quaternion.Euler(determinedRotation) * lastRotation;
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            Cells[0].transform.parent.localRotation = Quaternion.Lerp(lastRotation, nextRotation, timer / duration);
        }
        Cells[0].transform.parent.localRotation = nextRotation;
        CannotPress = false;
    }

    private IEnumerator ResetCube(float duration = 0.15f)
    {
        CannotPress = true;
        var temp = InputBoard.ToList();
        InputBoard = InitialBoard.ToList();
        var original = Cells[0].transform.parent.localEulerAngles;
        if (Cells[0].transform.parent.localEulerAngles.x != 0 || Cells[0].transform.parent.localEulerAngles.y != 0 || Cells[0].transform.parent.localEulerAngles.z != 0)
            Audio.PlaySoundAtTransform("reset", Cells[0].transform.parent);
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            Cells[0].transform.parent.localEulerAngles = Vector3.Lerp(original, Vector3.zero, timer / duration);
            for (int i = 0; i < Cells.Length; i++)
                Cells[i].GetComponent<MeshRenderer>().material.color = Color.Lerp(temp[i] ? CellColours[1] : CellColours[0], InputBoard[i] ? CellColours[1] : CellColours[0], timer / duration);
        }
        Cells[0].transform.parent.localEulerAngles = Vector3.zero;
        for (int i = 0; i < Cells.Length; i++)
            Cells[i].GetComponent<MeshRenderer>().material.color = InputBoard[i] ? CellColours[1] : CellColours[0];
        CannotPress = false;
    }

    private IEnumerator ClearCube(float duration = 0.15f)
    {
        CannotPress = true;
        var temp = InputBoard.ToList();
        InputBoard = InitialBoard.Select(x => false).ToList();
        var original = Cells[0].transform.parent.localEulerAngles;
        if (Cells[0].transform.parent.localEulerAngles.x != 0 || Cells[0].transform.parent.localEulerAngles.y != 0 || Cells[0].transform.parent.localEulerAngles.z != 0)
            Audio.PlaySoundAtTransform("reset", Cells[0].transform.parent);
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            Cells[0].transform.parent.localEulerAngles = Vector3.Lerp(original, Vector3.zero, timer / duration);
            for (int i = 0; i < Cells.Length; i++)
                Cells[i].GetComponent<MeshRenderer>().material.color = Color.Lerp(temp[i] ? CellColours[1] : CellColours[0], CellColours[0], timer / duration);
        }
        Cells[0].transform.parent.localEulerAngles = Vector3.zero;
        for (int i = 0; i < Cells.Length; i++)
            Cells[i].GetComponent<MeshRenderer>().material.color = CellColours[0];
        CannotPress = false;
    }

    private IEnumerator Solve(float duration = 0.15f)
    {
        Module.HandlePass();
        Solved = true;
        CannotPress = true;
        Debug.LogFormat("[Game of Life 3D #{0}] The answer you submitted was correct. Module solved!", _moduleID);
        Audio.PlaySoundAtTransform("solve", Cells[0].transform.parent);
        for (int i = 0; i < Cells.Length; i++)
            Cells[i].GetComponent<MeshRenderer>().material.color = CellColours[1];
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            Cells[0].transform.parent.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, timer / duration);
        }
        Cells[0].transform.parent.localScale = Vector3.zero;
    }

#pragma warning disable 414
    private string TwitchHelpMessage = "Use '!{0} ABCDE/#*' to press the buttons labelled A-E, press the button labelled \"RESET\", press the button labelled \"CLEAR\", then press the button labelled \"SUBMIT\". Use '!{0} <URDL' to press the up, right, down and left arrows.";
    private bool TwitchPlaysActive = false;
#pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        TwitchPlaysActive = true;
        SetLettersActive(true);
        command = command.ToLowerInvariant();
        if (command.First() == '<')
        {
            var validCommands = "urdl<";
            foreach (char character in command)
                if (!validCommands.Contains(character))
                {
                    yield return "sendtochaterror Invalid command.";
                    yield break;
                }
            yield return null;
            foreach (char character in command.Replace("<", ""))
            {
                Arrows[validCommands.IndexOf(character)].OnInteract();
                float reference = Time.time;
                yield return new WaitUntil(() => Time.time - reference > 0.075f && !CannotPress);
            }
        }
        else
        {
            var validCommands = "abcdefghijklmnopqrstuvwxyz/#*";
            foreach (char character in command)
                if (!validCommands.Contains(character))
                {
                    yield return "sendtochaterror Invalid command.";
                    yield break;
                }
            yield return null;
            foreach (char character in command)
            {
                if (validCommands.IndexOf(character) < 26)
                    Cells[validCommands.IndexOf(character)].OnInteract();
                else
                    Buttons[validCommands.IndexOf(character) - 26].OnInteract();
                float reference = Time.time;
                yield return new WaitUntil(() => Solved || (Time.time - reference > 0.075f && !CannotPress));
            }
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        while (!Solved)
        {
            for (int i = 0; i < Cells.Length; i++)
                if (Answer[i] != InputBoard[i])
                {
                    Cells[i].OnInteract();
                    float reference = Time.time;
                    yield return new WaitUntil(() => Time.time - reference > 0.075f);
                }
            if (InputBoard.Where((x, ix) => x == Answer[ix]).Count() == InputBoard.Count())
            {
                yield return null;
                Buttons[2].OnInteract();
            }
        }
    }
}