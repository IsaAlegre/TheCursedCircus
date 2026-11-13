using Godot;

/// <summary>
/// NPC_Controller (Fase 4): comportamiento autónomo del NPC.
/// Patrulla entre puntos (Marker2D colocados en el nivel), espera en cada uno
/// y, mientras el jugador le habla, se queda quieto mirándolo.
///
/// Equivalencias con Unity:
///   GameObject + Rigidbody2D (Kinematic) → CharacterBody2D (este nodo)
///   SpriteRenderer                       → nodo hijo "Sprite" (Sprite2D o AnimatedSprite2D)
///   BoxCollider2D                        → nodo hijo CollisionShape2D con RectangleShape2D
///   Transform[] patrolPoints             → PuntosPatrulla (Marker2D)
/// </summary>
public partial class NPC_Controller : CharacterBody2D
{
	[Export] public float Velocidad = 60f;
	/// <summary>Arrastrar aquí los Marker2D del nivel (PuntoA, PuntoB...).</summary>
	[Export] public Godot.Collections.Array<Node2D> PuntosPatrulla = new();
	[Export] public float TiempoEspera = 2f;
	[Export] public float Gravedad = 1200f;
	/// <summary>Flotación suave del sprite (personaje fantasmal).</summary>
	[Export] public bool Flotar = true;
	[Export] public float AlturaFlotacion = 6f;
	/// <summary>Si está desactivado, deja de flotar y se queda quieta mientras habla.</summary>
	[Export] public bool FlotarAlHablar = false;
	/// <summary>Marcar según hacia dónde mira el dibujo original del sprite.</summary>
	[Export] public bool SpriteMiraALaDerecha = true;
	/// <summary>Opcional: imagen del NPC mirando de frente, que se muestra mientras habla.
	/// Si queda vacío, se carga sola res://assets/personajes/ofelia_frente.png (si existe).
	/// Se escala sola para quedar del mismo alto que la imagen normal.</summary>
	[Export] public Texture2D ImagenDeFrente;

	private int _indicePunto = 0;
	private float _tiempoEsperado = 0f;
	private bool _esperando = false;
	private bool _conversando = false;
	private Node2D _interlocutor;
	private Node2D _sprite;
	private Vector2 _posicionSprite;
	private float _t;
	private Texture2D _imagenNormal;
	private Vector2 _escalaNormal = Vector2.One;
	private const string RutaFrentePorDefecto = "res://assets/personajes/ofelia_frente.png";

	public override void _Ready()
	{
		AddToGroup("npc");
		// Capa 4 = NPCs. Solo choca con el mundo (capa 1): el Mago y los enemigos lo atraviesan.
		CollisionLayer = 0;
		SetCollisionLayerValue(4, true);
		CollisionMask = 0;
		SetCollisionMaskValue(1, true);

		_sprite = GetNodeOrNull<Node2D>("Sprite");
		if (_sprite != null)
		{
			_posicionSprite = _sprite.Position;
			_escalaNormal = _sprite.Scale;
		}
		if (_sprite is Sprite2D s2d)
			_imagenNormal = s2d.Texture;
		if (ImagenDeFrente == null && ResourceLoader.Exists(RutaFrentePorDefecto))
			ImagenDeFrente = GD.Load<Texture2D>(RutaFrentePorDefecto);
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_t += dt;

		Vector2 velocidad = Velocity;
		if (!IsOnFloor())
			velocidad.Y += Gravedad * dt;
		velocidad.X = 0f;

		if (_conversando && !TieneImagenDeFrente())
			MirarHacia(PosicionDe(_interlocutor).X - GlobalPosition.X);
		else if (PuntosPatrulla.Count > 0)
			velocidad.X = Patrullar(dt);

		Velocity = velocidad;
		MoveAndSlide();

		// Si choca contra una pared, pasa al siguiente punto.
		if (!_conversando && !_esperando && IsOnWall())
			EmpezarEspera();

		Animar(velocidad.X);
	}

	private float Patrullar(float dt)
	{
		if (_esperando)
		{
			_tiempoEsperado += dt;
			if (_tiempoEsperado >= TiempoEspera)
			{
				_esperando = false;
				_indicePunto = (_indicePunto + 1) % PuntosPatrulla.Count;
			}
			return 0f;
		}

		Node2D objetivo = PuntosPatrulla[_indicePunto];
		if (objetivo == null)
			return 0f;

		float distancia = objetivo.GlobalPosition.X - GlobalPosition.X;
		if (Mathf.Abs(distancia) < 4f)
		{
			EmpezarEspera();
			return 0f;
		}
		float direccion = Mathf.Sign(distancia);
		MirarHacia(direccion);
		return direccion * Velocidad;
	}

	private void EmpezarEspera()
	{
		_esperando = true;
		_tiempoEsperado = 0f;
	}

	// ------------------------------------------------------------------
	// Lo llama NPC_Interaction al abrir y cerrar el diálogo
	// ------------------------------------------------------------------

	public void EmpezarConversacion(Node2D jugador)
	{
		_conversando = true;
		_interlocutor = jugador;
		if (TieneImagenDeFrente() && _sprite is Sprite2D sprite)
		{
			sprite.Texture = ImagenDeFrente;
			sprite.FlipH = false;
			float proporcion = (float)_imagenNormal.GetHeight() / ImagenDeFrente.GetHeight();
			sprite.Scale = _escalaNormal * proporcion;
		}
		else if (_sprite is AnimatedSprite2D animado && animado.SpriteFrames != null
				 && animado.SpriteFrames.HasAnimation("frente"))
		{
			animado.FlipH = false;
			animado.Play("frente");
		}
	}

	public void TerminarConversacion()
	{
		_conversando = false;
		_interlocutor = null;
		if (TieneImagenDeFrente() && _sprite is Sprite2D sprite)
		{
			sprite.Texture = _imagenNormal;
			sprite.Scale = _escalaNormal;
		}
	}

	/// <summary>Imagen normal del NPC (la usa NPC_Interaction para recortar el retrato).</summary>
	public Texture2D ImagenNormal => _imagenNormal;

	private bool TieneImagenDeFrente()
	{
		return ImagenDeFrente != null && _imagenNormal != null;
	}

	// ------------------------------------------------------------------
	// Presentación
	// ------------------------------------------------------------------

	/// <summary>El Mago (GDScript) expone centro(): el centro real de su cuerpo.</summary>
	private static Vector2 PosicionDe(Node2D nodo)
	{
		if (nodo == null || !IsInstanceValid(nodo))
			return Vector2.Zero;
		return nodo.HasMethod("centro") ? nodo.Call("centro").AsVector2() : nodo.GlobalPosition;
	}

	private void MirarHacia(float direccionX)
	{
		if (Mathf.IsZeroApprox(direccionX))
			return;
		bool voltear = (direccionX < 0f) == SpriteMiraALaDerecha;
		if (_sprite is Sprite2D sprite)
			sprite.FlipH = voltear;
		else if (_sprite is AnimatedSprite2D animado)
			animado.FlipH = voltear;
	}

	private void Animar(float velocidadX)
	{
		if (_sprite == null)
			return;
		bool flotando = Flotar && (FlotarAlHablar || !_conversando);
		Vector2 destino = flotando
			? _posicionSprite + new Vector2(0f, Mathf.Sin(_t * 2f) * AlturaFlotacion)
			: _posicionSprite;
		// Se acomoda suave en vez de saltar de golpe al empezar o terminar de hablar.
		_sprite.Position = _sprite.Position.Lerp(destino, 0.2f);

		if (_sprite is AnimatedSprite2D animado && animado.SpriteFrames != null && !_conversando)
		{
			string nombre = Mathf.Abs(velocidadX) > 1f ? "caminar" : "quieto";
			if (animado.SpriteFrames.HasAnimation(nombre))
				animado.Play(nombre);
		}
	}
}
