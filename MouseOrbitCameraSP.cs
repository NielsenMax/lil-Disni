using UnityEngine;
using System.Collections;

public class MouseOrbitCameraSP : MonoBehaviour {
	
	public float eyesHeight = 0.6f;
	public float cameraMaxDistance = 1;
	
	public float xSpeed = 250.0f;
	public float ySpeed = 120.0f;
	
	public float yMaxLimit = 50;
	public float yMinLimit = -20;
	
	public bool enableMoveOutSmooth = true;
	public float moveOutStartStep = 0.01f;
	
	public bool enableMoveOutIncrementalStep = true;
	public float moveOutMultiplierStep = 0.1f;
	
	private Transform _mainCamera;
	
	private float _moveOutStepMultiplier;
	private float _temp;
	
	private float _x = 0.0f;
	private float _y = 0.0f;
	
	private float _relativeDistance;
	
	// Use this for initialization
	void Start () 
	{
		_mainCamera = Camera.main.transform;
		
		Vector3 angles = _mainCamera.eulerAngles;
		
		_x = angles.y;
		_y = angles.x;
		
		_relativeDistance = cameraMaxDistance;
		
		// Make the rigid body not change rotation
		if (rigidbody)
			rigidbody.freezeRotation = true;
	}
	
	// Update is called once per frame
	void Update () 
	{
		_x += Input.GetAxis("Mouse X") * xSpeed * 0.02f;
		_y -= Input.GetAxis("Mouse Y") * ySpeed * 0.02f;
		
		_y = ClampAngle(_y, yMinLimit, yMaxLimit);
		
		Quaternion rotation = Quaternion.Euler(_y, _x, 0);
		
		//Actualiza el valor de _relativeDistance
		GetWallSafeDistance();
		
		Vector3 position = rotation * new Vector3(0.0f, eyesHeight, -_relativeDistance) + transform.position;
		
		_mainCamera.rotation = rotation;
		_mainCamera.position = position;
	}
	
	void GetWallSafeDistance()
	{
		RaycastHit hit;
		
		//Obtengo las coordenadas del punto de origen en los ojos del jugador
		Vector3 originPlayer = transform.position + new Vector3(0, eyesHeight, 0);
		
		//Obtengo el vector que apunta directo a la camara
		Vector3 targetCamera = _mainCamera.position - originPlayer;
		
		//Debug.DrawRay(originPlayer, targetCamera);
		
		//Verifico si el rayo colisiona con alguna pared
		if(Physics.Raycast(originPlayer, targetCamera, out hit))
		{
			if(hit.collider.tag == "LABHYRINT" ||
			   hit.collider.tag == "DOOR" ||
			   hit.collider.tag == "RAMP" ||
			   hit.collider.tag == "DOOR" || 
			   hit.collider.tag == "SMALLBURGER" ||
			   hit.collider.tag == "BIGBURGER")
			{
				if(hit.distance < cameraMaxDistance)
					_relativeDistance = hit.distance - 0.01f;
				else
					_relativeDistance = cameraMaxDistance;
			}
		}
		else
		{
			if(_relativeDistance < cameraMaxDistance)
			{
				if(enableMoveOutSmooth && (cameraMaxDistance - _relativeDistance) > moveOutStartStep)
				{
					if(enableMoveOutIncrementalStep)
					{
						//INI - Genero un multiplicador que se incremente en moveOutMultiplierStep por segundo 
						_temp += moveOutMultiplierStep * Time.smoothDeltaTime;
						_moveOutStepMultiplier = 1 + _temp;
						//FIN
						
						_relativeDistance += moveOutStartStep * _moveOutStepMultiplier;
					}
					else
					{
						_relativeDistance += moveOutStartStep;
					}
				}
				else
				{
					_relativeDistance = cameraMaxDistance;
				}
			}
			else
			{
				_relativeDistance = cameraMaxDistance;
			}
		}
	}
	
	float ClampAngle(float angle, float min, float max)
	{
		if (angle < -360)
			angle += 360;
		if (angle > 360)
			angle -= 360;
		
		return Mathf.Clamp (angle, min, max);
	}
}