using UnityEngine;
using System.Collections;

public class ThirdPersonControllerSP : MonoBehaviour
{
	public AnimationClip idleAnimation;
	public AnimationClip walkAnimation;
	public AnimationClip runAnimation;
	public AnimationClip jumpPoseAnimation;
	
	public float walkMaxAnimationSpeed = 0.75f;
	public float trotMaxAnimationSpeed = 1.0f;
	public float runMaxAnimationSpeed = 1.0f;
	public float jumpAnimationSpeed = 1.15f;
	public float landAnimationSpeed = 1.0f;
	
	private Animation _animation;
	
	private CharacterAction _characterState;
	
	// Se setea la velocidad de la caminata
	public float walkSpeed= 2.0f;
	// Transcurrido el tiempo de caminata seteado en trotAfterSeconds se trota a la velocidad "trotSpeed" 
	public float trotSpeed= 4.0f;
	// Cuando se presiona shift se comienza a correr. Se setea la velocidad:
	public float runSpeed= 6.0f;
	
	public float inAirControlAcceleration= 3.0f;
	
	// Que tan alto se salta
	public float jumpHeight= 0.5f;
	
	// Se setea la gravedad
	public float gravity= 20.0f;
	// Se setea la gravedad en modo descenso controlado (controlled descent)
	public float speedSmoothing= 10.0f;
	public float rotateSpeed= 500.0f;
	public float trotAfterSeconds= 3.0f;
	// Variable que indica si esta permitido saltar.
	public bool canJump= true;
	
	private float jumpRepeatTime= 0.05f;
	private float jumpTimeout= 0.15f;
	private float groundedTimeout= 0.25f;
	
	// La camara no comienza a seguir al personaje inmediatamente sino que espera una fraccion 
	//	de segundo para evitar el exceso de girar alrededor
	private float lockCameraTimer= 0.0f;
	
	// La dirección de movimiento actual en plano x-z
	private Vector3 moveDirection= Vector3.zero;
	// La velocidad vertical actual (0 por que esta sobre el suelo)
	private float verticalSpeed= 0.0f;
	// La velocidad de movimiento actual en plano x-z (quieto)
	private float moveSpeed= 0.0f;
	
	// Bandera de colision provista por el controlador de movimiento
	private CollisionFlags collisionFlags; 
	
	// Variables que indican si se presiono la tecla de saltar
	private bool jumping= false;
	private bool jumpingReachedApex= false;
	
	// Variable que indica si nos estamos moviendo para atras (para bloquear la camara y no rotar 180 grados)
	private bool movingBack= false;
	// Variable que indica si el jugador se esta moviendo en alguna direccion
	private bool isMoving= false;
	// Cuando el usuario comenzo a caminar (usado para comenzar a trotar luego de un tiempo
	private float walkTimeStart= 0.0f;
	// Ultima vez que se presiono el boton saltar 
	private float lastJumpButtonTime= -10.0f;
	// Ultima vez que se realizo y finalizo el salto
	private float lastJumpTime= -1.0f;
	
	// La altura desde la cual el personaje salta usada para determinar durante cuanto tiempo aplicar potencia extra de salto
	private float lastJumpStartHeight= 0.0f;
	
	private Vector3 inAirVelocity= Vector3.zero;
	
	private float lastGroundedTime= 0.0f;
	
	private bool isControllable= true;
	
	void  Awake()
	{
		moveDirection = transform.TransformDirection(Vector3.forward);
		
		_animation = GetComponent<Animation>();
		if(!_animation)
			Debug.Log("The character you would like to control doesn't have animations. Moving her might look weird.");
		
		if(!idleAnimation) {
			_animation = null;
			Debug.Log("No idle animation found. Turning off animations.");
		}
		if(!walkAnimation) {
			_animation = null;
			Debug.Log("No walk animation found. Turning off animations.");
		}
		if(!runAnimation) {
			_animation = null;
			Debug.Log("No run animation found. Turning off animations.");
		}
		if(!jumpPoseAnimation && canJump) {
			_animation = null;
			Debug.Log("No jump animation found and the character has canJump enabled. Turning off animations.");
		}	
	}
	
	void UpdateSmoothedMovementDirection()
	{
		Transform cameraTransform = Camera.main.transform;
		bool grounded= IsGrounded();
		
		// Vector "forward" relativo a la camara a lo largo del plano x-z.	
		Vector3 forward = cameraTransform.TransformDirection(Vector3.forward);
		forward.y = 0;
		forward = forward.normalized;
		
		// Vector "right" relativo a la camera, siempre demanera ortogonal al vector forward
		Vector3 right= new Vector3(forward.z, 0, -forward.x);
		
		float v= Input.GetAxisRaw("Vertical");
		float h= Input.GetAxisRaw("Horizontal");
		
		// Se verifica si se esta moviendo hacia atras o mirando hacia atras
		if (v < -0.2f)
			movingBack = true;
		else
			movingBack = false;
		
		bool wasMoving= isMoving;
		isMoving = Mathf.Abs (h) > 0.1f || Mathf.Abs (v) > 0.1f;
		
		// Direccion destino (relativa a la camara)
		Vector3 targetDirection= h * right + v * forward;
		
		// Controles en tierra
		if (grounded)
		{
			// Se bloquea la camara por un pequeño periodo cuando se transiciona el movimiento y se queda quieto
			lockCameraTimer += Time.deltaTime;
			if (isMoving != wasMoving)
				lockCameraTimer = 0.0f;
			
			// Almacenamos direccion y velocidad de movimiento de manera independiente
			// para que cuando el personaje permanezca quieto, se tenga una dirección valida
			// moveDirection solo se actualiza si hay entrada del usuario
			if (targetDirection != Vector3.zero)
			{
				// Si la velocidad de movimiento es muy baja, simplmente se saca a la direccion de destino
				if (moveSpeed < walkSpeed * 0.9f && grounded)
				{
					moveDirection = targetDirection.normalized;
				}
				// De otra manera, suavemente gira hacia ella
				else
				{
					moveDirection = Vector3.RotateTowards(moveDirection, targetDirection, rotateSpeed * Mathf.Deg2Rad * Time.deltaTime, 1000);
					moveDirection = moveDirection.normalized;
				}
			}
			
			// Suavemente se modifica la velocidad en función de la dirección de destino
			float curSmooth= speedSmoothing * Time.deltaTime;
			
			// Se elige la velocidad
			// Se intenta soportar velocidad analoga pero hay que asegurarse que no se puede caminar mas rapido de manera diagonal que yendo derecho o de costado
			float targetSpeed= Mathf.Min(targetDirection.magnitude, 1.0f);
			
			_characterState = CharacterAction.Idle;
			
			// Se setea la velocidad elegida
			if (Input.GetKey (KeyCode.LeftShift) || Input.GetKey (KeyCode.RightShift))
			{
				targetSpeed *= runSpeed;
				_characterState = CharacterAction.Running;
			}
			else if (Time.time - trotAfterSeconds > walkTimeStart)
			{
				targetSpeed *= trotSpeed;
				_characterState = CharacterAction.Trotting;
			}
			else if(Input.GetKey (KeyCode.W) ||
			        Input.GetKey (KeyCode.S) ||
			        Input.GetKey (KeyCode.A) ||
			        Input.GetKey (KeyCode.D) ) 
			{
				targetSpeed *= walkSpeed;
				_characterState = CharacterAction.Walking;
			}
			else
			{
				_characterState = CharacterAction.Idle;
			}
			
			moveSpeed = Mathf.Lerp(moveSpeed, targetSpeed, curSmooth);
			
			// Se resetea el tiempo de inicio de caminara cuando se reduce la velocidad 
			if (moveSpeed < walkSpeed * 0.3f)
				walkTimeStart = Time.time;
		}
		// Controles mientras se esta en el aire
		else
		{
			// Se bloquea la camara mientras esta en el aire. 
			if (jumping)
				lockCameraTimer = 0.0f;
			
			if (isMoving)
				inAirVelocity += targetDirection.normalized * Time.deltaTime * inAirControlAcceleration;
		}
	}
	
	void ApplyJumping(){
		// Evita que se salte rapidamente un salto tras otro.
		if (lastJumpTime + jumpRepeatTime > Time.time)
			return;
		
		if (IsGrounded()) {
			// Se salta: si se presiona el boton y si transcurrio el tiempo de espera 
			//  para poder pulsar el botón ligeramente antes de volver al piso.
			if (canJump && Time.time < lastJumpButtonTime + jumpTimeout) {
				verticalSpeed = CalculateJumpVerticalSpeed (jumpHeight);
				SendMessage("DidJump", SendMessageOptions.DontRequireReceiver);
			}
		}
	}
	
	
	void ApplyGravity()
	{
		// Si el personaje no es controlable, no se mueve
		if (isControllable)
		{
			// Se aplica gravedad
			bool jumpButton= Input.GetButton("Jump");
			
			
			// Cuando se alcanza la maxima altura en el salto, se envia un mensaje.
			if (jumping && !jumpingReachedApex && verticalSpeed <= 0.0f)
			{
				jumpingReachedApex = true;
				SendMessage("DidJumpReachApex", SendMessageOptions.DontRequireReceiver);
			}
			
			// Si llego a tierra, vuelve velocidad vertical a 0.
			if (IsGrounded ())
				verticalSpeed = 0.0f;
			else
				verticalSpeed -= gravity * Time.deltaTime;
		}
	}
	
	public float CalculateJumpVerticalSpeed( float targetJumpHeight  )
	{
		//  A partir del peso y la gravedad se calcula la velocidad hacia arriba hasta alcanzar altura maxima
		return Mathf.Sqrt(2 * targetJumpHeight * gravity);
	}
	
	public void DidJump()
	{
		jumping = true;
		jumpingReachedApex = false;
		lastJumpTime = Time.time;
		lastJumpStartHeight = transform.position.y;
		lastJumpButtonTime = -10;
		
		_characterState = CharacterAction.Jumping;
	}
	
	void Update()
	{
		
		if (!isControllable)
		{
			// Se matan los controles de entrada si no es controlable
			Input.ResetInputAxes();
		}
		
		// Si se presiono saltar
		if (Input.GetButtonDown ("Jump"))
		{
			lastJumpButtonTime = Time.time;
		}
		
		// Actualiza la direccion de movimiento.
		UpdateSmoothedMovementDirection();
		
		// Se aplica gravedad
		// Salto de extra power modifica la gravedad
		// Modo controlledDescent modifica la gravedad
		ApplyGravity ();
		
		// Se aplica la logica del salto
		ApplyJumping ();
		
		// Se calcula movimiento actual
		Vector3 movement= moveDirection * moveSpeed + new Vector3 (0, verticalSpeed, 0) + inAirVelocity;
		movement *= Time.deltaTime;
		
		// Se mueve el controller
		CharacterController controller = GetComponent<CharacterController>();
		collisionFlags = controller.Move(movement);
		
		// Animaciones
		if(_animation) {
			if(_characterState == CharacterAction.Jumping) 
			{
				if(!jumpingReachedApex) {
					_animation[jumpPoseAnimation.name].speed = jumpAnimationSpeed;
					_animation[jumpPoseAnimation.name].wrapMode = WrapMode.ClampForever;
					_animation.CrossFade(jumpPoseAnimation.name);
				} else {
					_animation[jumpPoseAnimation.name].speed = -landAnimationSpeed;
					_animation[jumpPoseAnimation.name].wrapMode = WrapMode.ClampForever;
					_animation.CrossFade(jumpPoseAnimation.name);
				}
			} 
			else 
			{
				if(controller.velocity.sqrMagnitude < 0.1f) {
					_animation.CrossFade(idleAnimation.name);
				}
				else 
				{
					if(_characterState == CharacterAction.Running) {
						_animation[runAnimation.name].speed = Mathf.Clamp(controller.velocity.magnitude, 0.0f, runMaxAnimationSpeed);
						_animation.CrossFade(runAnimation.name);
					}
					else if(_characterState == CharacterAction.Trotting) {
						_animation[walkAnimation.name].speed = Mathf.Clamp(controller.velocity.magnitude, 0.0f, trotMaxAnimationSpeed);
						_animation.CrossFade(walkAnimation.name);
					}
					else if(_characterState == CharacterAction.Walking) {
						_animation[walkAnimation.name].speed = Mathf.Clamp(controller.velocity.magnitude, 0.0f, walkMaxAnimationSpeed);
						_animation.CrossFade(walkAnimation.name);
					}
					
				}
			}
		}
		
		// Se setea la rotacion a la direccion de movimiento
		if (IsGrounded())
		{
			transform.rotation = Quaternion.LookRotation(moveDirection);
		}	
		else
		{
			Vector3 xzMove= movement;
			xzMove.y = 0;
			if (xzMove.sqrMagnitude > 0.001f)
			{
				transform.rotation = Quaternion.LookRotation(xzMove);
			}
		}	
		
		// Se esta en modo salto pero recien volviendo al piso
		if (IsGrounded())
		{
			lastGroundedTime = Time.time;
			inAirVelocity = Vector3.zero;
			if (jumping)
			{
				jumping = false;
				SendMessage("DidLand", SendMessageOptions.DontRequireReceiver);
			}
		}
	}
	
	void OnControllerColliderHit( ControllerColliderHit hit   )
	{
		if (hit.moveDirection.y > 0.01f) 
			return;
	}
	
	public float GetSpeed()
	{
		return moveSpeed;
	}
	
	public bool IsJumping()
	{
		return jumping;
	}
	
	public bool IsGrounded()
	{
		return (collisionFlags & CollisionFlags.CollidedBelow) != 0;
	}
	
	public Vector3 GetDirection()
	{
		return moveDirection;
	}
	
	public bool IsMovingBackwards()
	{
		return movingBack;
	}
	
	public float GetLockCameraTimer()
	{
		return lockCameraTimer;
	}
	
	public bool IsMoving()
	{
		return Mathf.Abs(Input.GetAxisRaw("Vertical")) + Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.5f;
	}
	
	public bool HasJumpReachedApex()
	{
		return jumpingReachedApex;
	}
	
	public bool IsGroundedWithTimeout()
	{
		return lastGroundedTime + groundedTimeout > Time.time;
	}
	
	public void Reset()
	{
		gameObject.tag = "Player";
	}
	
	public CharacterAction GetCharacterState()
	{
		return _characterState;
	}
}