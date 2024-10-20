using Cysharp.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;

public class CheckersVisualizer : MonoBehaviour
{
    public static readonly GameObject[] _playerFigures = new GameObject[2];

    [SerializeField] private float _figureSize;
    [SerializeField] private AnimationCurve _jumpCurve;
    [SerializeField] private float _jumpDuration;
    [SerializeField] private float _jumpHeigh;
    [SerializeField] private List<GameObject> _figurePrefabs;
    [SerializeField] private GameObject _crownPrefab;
    [SerializeField] private GameObject _selectionCube;
    [SerializeField] private CheckersLogic _logic;

    private readonly Transform[,] _figureTransforms = new Transform[8, 8];
    private Transform _figureTransform;
    private Vector3 _endPosition;

    private void Awake()
    {
        int index;

        if (_playerFigures[0] == null)
        {
            index = Random.Range(0, _figurePrefabs.Count);
            _playerFigures[0] = _figurePrefabs[index];
        }

        _figurePrefabs.Remove(_playerFigures[0]);

        index = Random.Range(0, _figurePrefabs.Count);
        _playerFigures[1] = _figurePrefabs[index];

        _selectionCube.SetActive(false);
    }

    private void OnEnable()
    {
        _logic.FigurePlaced += PlaceFigure;
        _logic.FigureSelected += ChangeSelection;
        _logic.FigureMoved += Move;
        _logic.FigureChopped += Chop;
        _logic.DamCreated += CreateDam;
        _logic.GameEnding += PlayEndingAnimation;
    }

    private void OnDisable()
    {
        _logic.FigurePlaced -= PlaceFigure;
        _logic.FigureSelected -= ChangeSelection;
        _logic.FigureMoved -= Move;
        _logic.FigureChopped -= Chop;
        _logic.DamCreated -= CreateDam;
        _logic.GameEnding -= PlayEndingAnimation;
    }

    private async UniTaskVoid PlaceFigure(int i, int j, int index)
    {
        Vector3 position = CoordinateTranslator.Indexes2Position(i, j);
        var task = InstantiateAsync(_playerFigures[index], position, Quaternion.identity);
        await task;
        GameObject newFigure = task.Result[0];
        newFigure.name = _playerFigures[index].name;
        newFigure.transform.localScale *= _figureSize;
        _figureTransforms[i, j] = newFigure.transform;
    }

    private void ChangeSelection(List<int> indexes, bool shoodSelect)
    {
        if (shoodSelect)
        {
            (int i, int j) = (indexes[0], indexes[1]);

            Vector3 position = CoordinateTranslator.Indexes2Position(i, j);
            _selectionCube.transform.localPosition = position;
        }
        _selectionCube.SetActive(shoodSelect);
    }

    private async UniTask Move(List<int> moveIndex) 
    {
        (int i, int j, int iDelta, int jDelta) = (moveIndex[0], moveIndex[1], moveIndex[2], moveIndex[3]);

        _figureTransform = _figureTransforms[i, j];

        Vector3 startPosition = _figureTransform.position;
        _endPosition = CoordinateTranslator.Indexes2Position(i + iDelta, j + jDelta);
        float distance = Vector3.Distance(startPosition, _endPosition);
        float moveDuration = distance / _logic.MoveSpeed;

        await MoveFigure(startPosition, _endPosition, moveDuration);

        _figureTransforms[i + iDelta, j + jDelta] = _figureTransform;
        _figureTransforms[i, j] = null;
    }

    private async UniTask MoveFigure(Vector3 startPosition, Vector3 endPosition, float moveDuration)
    {
        float elapsedTime = 0f;
        float t;

        while (elapsedTime < moveDuration)
        {
            t = elapsedTime / moveDuration;
            _figureTransform.position = Vector3.Lerp(startPosition, endPosition, t);

            elapsedTime += Time.deltaTime;

            await UniTask.Yield();
        }

        _figureTransform.position = endPosition;
    }

    private async UniTaskVoid Chop(List<int> moveIndex, float chopDelay)
    {
        (int rivalI, int rivalJ) = (moveIndex[4], moveIndex[5]);

        await UniTask.WaitForSeconds(chopDelay);

        Destroy(_figureTransforms[rivalI, rivalJ].gameObject);
        _figureTransforms[rivalI, rivalJ] = null;
    }

    private void CreateDam()
    {
        Transform childFigureTransform = _figureTransform.GetChild(0);
        Vector3 figurePosition = _figureTransform.position;
        Renderer renderer = childFigureTransform.GetComponent<Renderer>();
        Bounds bounds = renderer.bounds;
        Vector3 crownPosition = figurePosition + new Vector3(0, bounds.center.y * 0.6f + bounds.extents.y, 0);

        GameObject crown = Instantiate(_crownPrefab, crownPosition, Quaternion.Euler(-90, 0, 0));
        crown.transform.parent = childFigureTransform;
    }

    private async UniTask PlayEndingAnimation(int winnerTurn, float gameEndingDuration, CancellationToken token)
    {
        string winnerName = _playerFigures[winnerTurn - 1].name;
        List<UniTask> tasks = new();

        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                Transform figureTransform = _figureTransforms[i, j];

                if (figureTransform != null && figureTransform.name == winnerName)
                {
                    float startJumpDelay = Random.Range(0, gameEndingDuration);
                    tasks.Add(JumpFigure(figureTransform, startJumpDelay, token));
                }
            }
        }

        await UniTask.WhenAll(tasks);
    }

    private async UniTask JumpFigure(Transform figureTransform, float startJumpDelay, CancellationToken token)
    {
        await UniTask.WaitForSeconds(startJumpDelay, cancellationToken: token);

        Vector3 figurePosition = figureTransform.position;

        float expiredTime = 0f;

        while (expiredTime < _jumpDuration)
        {
            float progress = expiredTime / _jumpDuration;
            float currentY = _jumpHeigh * _jumpCurve.Evaluate(progress);
            figureTransform.position = new Vector3(figurePosition.x, currentY, figurePosition.z);
            expiredTime += Time.deltaTime;

            await UniTask.Yield(cancellationToken: token);
        }

        figureTransform.position = figurePosition;
    }
}
