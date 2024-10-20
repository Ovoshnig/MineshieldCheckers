using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraPositionSwitch : MonoBehaviour
{
    [SerializeField] private List<Vector3> _positions = new();
    [SerializeField] private List<Quaternion> _rotations = new();
    [SerializeField] private int _removeIndex;

    private Vector2 _direction;
    private int _currentIndex;

    private void Start()
    {
        _currentIndex = 0;
        transform.SetPositionAndRotation(_positions[_currentIndex], _rotations[_currentIndex]);
    }

    public void OnSwitchClick(InputAction.CallbackContext context)
    {
        _direction = context.action.ReadValue<Vector2>();

        if (_direction.x > 0)
            GoToNextCamera();
        else if (_direction.x < 0)
            GoToPreviousCamera();
    }

    [ContextMenu("Add new point")]
    private void AddTransform()
    {
        _positions.Add(transform.position);
        _rotations.Add(transform.rotation);
    }

    [ContextMenu("Remove point")]
    private void RemoveTransform()
    {
        _positions.RemoveAt(_removeIndex);
        _rotations.RemoveAt(_removeIndex);
    }

    private void GoToPreviousCamera()
    {
        if (_currentIndex > 0)
            _currentIndex--;
        else
            _currentIndex = _positions.Count - 1;

        transform.SetPositionAndRotation(_positions[_currentIndex], _rotations[_currentIndex]);
    }

    private void GoToNextCamera()
    {
        if (_currentIndex < _positions.Count - 1)
            _currentIndex++;
        else
            _currentIndex = 0;

        transform.SetPositionAndRotation(_positions[_currentIndex], _rotations[_currentIndex]);
    }
}
