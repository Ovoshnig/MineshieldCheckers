using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CheckersLogic))]
public class CheckersVisualizer : MonoBehaviour
{
    [SerializeField] private float _moveSpeed;
    [SerializeField] private float _figureSize;

    [SerializeField] private AnimationCurve _jumpCurve;
    [SerializeField] private int _jumpDuration;
    [SerializeField] private float _jumpHeigh;

    [SerializeField] private List<GameObject> _figurePrefabs;
    [SerializeField] private GameObject _crownPrefab;
    [SerializeField] private GameObject _selectionCube;

    private CheckersLogic _logic;

    public static readonly GameObject[] _playerFigures = new GameObject[2];
    private readonly Transform[,] _figureTransforms = new Transform[8, 8];

    private Transform _figureTransform;

    private const float _cellSize = 2.5f;

    private Vector3 _endPosition;

    private void OnValidate() => _logic ??= FindObjectOfType<CheckersLogic>();

    private void OnEnable()
    {
        _logic.FigurePlacedEvent += PlaceFigure;
        _logic.FigureSelectedEvent += ChangeSelection;
        _logic.FigureMovedEvent += Move;
        _logic.FigureChoppedEvent += Chop;
        _logic.DamCreatedEvent += CreateDam;
        _logic.GameEndedEvent += PlayEndingAnimation;
    }

    private void OnDisable()
    {
        _logic.FigurePlacedEvent -= PlaceFigure;
        _logic.FigureSelectedEvent -= ChangeSelection;
        _logic.FigureMovedEvent -= Move;
        _logic.FigureChoppedEvent -= Chop;
        _logic.DamCreatedEvent -= CreateDam;
        _logic.GameEndedEvent -= PlayEndingAnimation;
    }

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

    private Vector3 Indexes2Position(int i, int j)
    {
        float iFloat = i + 0.5f;
        float jFloat = j + 0.5f;

        iFloat -= 4f;
        jFloat -= 4f;

        iFloat *= _cellSize;
        jFloat *= _cellSize;

        Vector3 position = new(iFloat, 0f, jFloat);
        return position;
    }

    private async UniTaskVoid PlaceFigure(int i, int j, int index)
    {
        Vector3 position = Indexes2Position(i, j);
        var newFigure = Instantiate(_playerFigures[index], position, Quaternion.Euler(0, 0, 0));
        newFigure.name = _playerFigures[index].name;
        newFigure.transform.localScale *= _figureSize;

        await UniTask.Yield();

        _figureTransforms[i, j] = newFigure.transform;
    }

    private void ChangeSelection(List<int> indexes, bool shoodSelect)
    {
        if (shoodSelect)
        {
            var (i, j) = (indexes[0], indexes[1]);

            Vector3 position = Indexes2Position(i, j);
            _selectionCube.transform.localPosition = position;
        }
        _selectionCube.SetActive(shoodSelect);
    }

    private async UniTask Move(List<int> moveIndex) 
    {
        var (i, j, iDelta, jDelta) = (moveIndex[0], moveIndex[1], moveIndex[2], moveIndex[3]);

        _figureTransform = _figureTransforms[i, j];

        Vector3 startPosition = _figureTransform.position;
        _endPosition = Indexes2Position(i + iDelta, j + jDelta);
        float distance = Vector3.Distance(startPosition, _endPosition);
        float moveDuration = distance / _moveSpeed;

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

    private async UniTaskVoid Chop(List<int> moveIndex)
    {
        var (i, j, rivalI, rivalJ) = (moveIndex[0], moveIndex[1], moveIndex[4], moveIndex[5]);

        Vector3 startPosition = Indexes2Position(i, j);
        Vector3 rivalPosition = Indexes2Position(rivalI, rivalJ);
        float distance = Vector3.Distance(startPosition, rivalPosition);
        int removeDuration = (int)(1000 * (distance / _moveSpeed));

        await UniTask.Delay(removeDuration);

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

    private async UniTaskVoid PlayEndingAnimation(int winnerTurn, int gameEndingDuration)
    {
        string winnerName = _playerFigures[winnerTurn - 1].name;

        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                Transform figureTransform = _figureTransforms[i, j];

                if (figureTransform != null && figureTransform.name == winnerName)
                {
                    int startJumpDelay = Random.Range(0, gameEndingDuration);
                    JumpFigure(figureTransform, startJumpDelay).Forget();

                    await UniTask.Yield();
                }
            }
        }
    }

    private async UniTask JumpFigure(Transform figureTransform, int startJumpDelay)
    {
        await UniTask.Delay(startJumpDelay);

        Vector3 figurePosition = figureTransform.position;

        float expiredTime = 0f;

        while (expiredTime < _jumpDuration)
        {
            float progress = expiredTime / _jumpDuration;
            float currentY = _jumpHeigh * _jumpCurve.Evaluate(progress);
            figureTransform.position = new Vector3(figurePosition.x, currentY, figurePosition.z);
            expiredTime += Time.deltaTime;

            await UniTask.Yield();
        }
        figureTransform.position = figurePosition;
    }
}
