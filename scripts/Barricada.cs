using Godot;

/// <summary>
/// Barricada de Telones (tramo 5 del GDD): telones viejos y tablones que cierran el paso.
/// Solo el Hechizo de Fuego la quema; el Proyectil Arcano rebota sin efecto y, la primera vez,
/// aparece una pista. Una vez quemada queda guardada en la partida y no vuelve a aparecer.
/// Colocar el nodo con su origen apoyado en el piso. La colisión se crea por código.
/// </summary>
public partial class Barricada : StaticBody2D
{
	[Export] public float Ancho = 210f;
	[Export] public float Alto = 430f;
	/// <summary>Distancia a la que aparece el aviso de qué hacer.</summary>
	[Export] public float DistanciaAviso = 520f;
	[Export] public string Pista = "Los telones están empapados de brea... el fuego los consumiría.";
	/// <summary>Aviso cuando el Mago ya tiene el Fuego.</summary>
	[Export] public string AvisoConFuego = "Prendé fuego a los telones.";
	[Export] public float VolumenEfectosDb = -2f;

	private const string RutaTelon = "res://assets/muro_lona_1.png";
	private const string RutaFuente = "res://assets/fuentes/IMFellEnglish-Regular.ttf";
	private const string RutaSonidoFuego = "res://assets/audio/jefe/disparo_fuego.wav";
	private static readonly Color ColorFuego = new("FF5A00");
	private static readonly Color Rojo = new("E33224");
	private static readonly Color Amarillo = new("FFC247");
	private static readonly Color Madera = new("4A3426");
	private static readonly Color Tinta = new("151515");
	private static readonly Color BlancoHueso = new("F5F2EA");

	private Texture2D _telon;
	private Font _fuente;
	private CollisionShape2D _colision;
	private bool _quemandose = false;
	private float _quemado = 0f;     // 0 = intacta, 1 = consumida
	private float _t = 0f;
	private float _temblor = 0f;
	private float _alfaPista = 0f;
	private bool _pistaMostrada = false;
	private Node2D _jugador;
	private float _alfaAviso = 0f;
	private string _aviso = "";
	private bool _jugadorTieneFuego = false;
	private readonly System.Collections.Generic.List<Vector3> _brasas = new(); // x, y, vida
	private float _tBrasa = 0f;

	public override void _Ready()
	{
		// Si ya se quemó en esta partida, no vuelve a aparecer.
		Node partida = GetNodeOrNull("/root/Partida");
		if (partida != null && partida.Get("barricada_quemada").AsBool())
		{
			QueueFree();
			return;
		}

		CollisionLayer = 0;
		SetCollisionLayerValue(1, true); // mundo: frena al Mago, a los enemigos y a los proyectiles
		CollisionMask = 0;
		_colision = new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(Ancho * 0.6f, Alto) },
			Position = new Vector2(0f, -Alto * 0.5f),
		};
		AddChild(_colision);

		_telon = ResourceLoader.Exists(RutaTelon) ? GD.Load<Texture2D>(RutaTelon) : null;
		_fuente = ResourceLoader.Exists(RutaFuente) ? GD.Load<Font>(RutaFuente) : ThemeDB.FallbackFont;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_t += dt;
		_temblor = Mathf.MoveToward(_temblor, 0f, dt * 4f);
		ActualizarAviso(dt);
		ActualizarBrasas(dt);
		if (_quemandose)
		{
			_quemado = Mathf.MoveToward(_quemado, 1f, dt / 1.8f);
			if (_quemado >= 1f)
			{
				QueueFree();
				return;
			}
		}
		QueueRedraw();
	}

	/// <summary>
	/// Cuando el Mago se acerca, aparece qué hacer según lo que tenga:
	/// sin Fuego, la pista; con el Fuego, que lo use contra los telones.
	/// </summary>
	private void ActualizarAviso(float dt)
	{
		if (_jugador == null || !IsInstanceValid(_jugador))
			_jugador = GetTree().GetFirstNodeInGroup("jugador") as Node2D;
		bool cerca = false;
		if (_jugador != null && !_quemandose)
		{
			Vector2 posJugador = _jugador.HasMethod("centro") ? _jugador.Call("centro").AsVector2() : _jugador.GlobalPosition;
			cerca = Mathf.Abs(posJugador.X - GlobalPosition.X) < DistanciaAviso;
			_jugadorTieneFuego = _jugador.Get("tiene_fuego").AsBool();
			_aviso = _jugadorTieneFuego ? AvisoConFuego : Pista;
		}
		_alfaAviso = Mathf.MoveToward(_alfaAviso, cerca ? 1f : 0f, dt * 3f);
	}

	/// <summary>Brasas que suben desde el telón: con el Fuego en mano, muchas más (llaman la atención).</summary>
	private void ActualizarBrasas(float dt)
	{
		_tBrasa -= dt;
		float cada = _quemandose ? 0.02f : (_jugadorTieneFuego ? 0.08f : 0.35f);
		if (_tBrasa <= 0f && _quemado < 0.9f)
		{
			_tBrasa = cada;
			_brasas.Add(new Vector3((float)GD.RandRange(-Ancho * 0.5, Ancho * 0.5), -(float)GD.RandRange(0.0, Alto * (1f - _quemado)), 1f));
		}
		for (int i = _brasas.Count - 1; i >= 0; i--)
		{
			Vector3 b = _brasas[i];
			b.Y -= dt * 60f;
			b.X += Mathf.Sin(_t * 3f + i) * dt * 15f;
			b.Z -= dt * 0.6f;
			if (b.Z <= 0f)
				_brasas.RemoveAt(i);
			else
				_brasas[i] = b;
		}
	}

	/// <summary>Lo llaman los proyectiles del Mago al chocar.</summary>
	public bool recibir_danio(int cantidad, Node2D origen)
	{
		if (_quemandose)
			return false;
		Variant color = origen?.Get("color") ?? default;
		bool esFuego = color.VariantType == Variant.Type.Color && color.AsColor().IsEqualApprox(ColorFuego);
		if (esFuego)
		{
			Quemar();
			return true;
		}
		// El arcano no le hace nada: solo se sacude y da la pista una vez.
		_temblor = 1f;
		if (!_pistaMostrada)
		{
			_pistaMostrada = true;
			var tw = CreateTween();
			tw.TweenMethod(Callable.From<float>(v => _alfaPista = v), 0f, 1f, 0.4f);
			tw.TweenInterval(3.5f);
			tw.TweenMethod(Callable.From<float>(v => _alfaPista = v), 1f, 0f, 0.8f);
		}
		return false;
	}

	private void Quemar()
	{
		_quemandose = true;
		_alfaPista = 0f;
		_colision.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		GetNodeOrNull("/root/Partida")?.Set("barricada_quemada", true);
		if (ResourceLoader.Exists(RutaSonidoFuego))
		{
			var p = new AudioStreamPlayer2D
			{
				Stream = GD.Load<AudioStream>(RutaSonidoFuego),
				VolumeDb = VolumenEfectosDb,
				PitchScale = 0.7f,
			};
			GetParent().AddChild(p);
			p.GlobalPosition = GlobalPosition + new Vector2(0f, -Alto * 0.5f);
			p.Play();
			p.Finished += p.QueueFree;
		}
	}

	public override void _Draw()
	{
		float sacudida = Mathf.Sin(_t * 60f) * 4f * _temblor;
		DrawSetTransform(new Vector2(sacudida, 0f), 0f, Vector2.One);

		// Resplandor naranja que late cuando el Mago ya tiene el Fuego: "esto se quema".
		if (_jugadorTieneFuego && !_quemandose)
		{
			float pulso = 0.5f + 0.5f * Mathf.Sin(_t * 4f);
			DrawRect(new Rect2(-Ancho * 0.5f - 14f, -Alto - 14f, Ancho + 28f, Alto + 14f), new Color(ColorFuego, 0.10f + 0.12f * pulso));
			DrawRect(new Rect2(-Ancho * 0.5f - 6f, -Alto - 6f, Ancho + 12f, Alto + 6f), new Color(ColorFuego, 0.35f + 0.4f * pulso), false, 3f);
		}

		// El telón se carboniza y se consume de arriba hacia abajo.
		float alturaVisible = Alto * (1f - _quemado);
		var color = Colors.White.Lerp(new Color(0.15f, 0.08f, 0.05f), Mathf.Clamp(_quemado * 2f, 0f, 1f));
		if (_telon != null && alturaVisible > 1f)
		{
			float fraccion = alturaVisible / Alto;
			var region = new Rect2(0f, _telon.GetHeight() * (1f - fraccion), _telon.GetWidth(), _telon.GetHeight() * fraccion);
			DrawTextureRectRegion(_telon, new Rect2(-Ancho * 0.5f, -alturaVisible, Ancho, alturaVisible), region, color);
		}

		// Tablones clavados en cruz (se queman junto con el telón).
		if (_quemado < 0.6f)
		{
			var madera = Madera.Lerp(Tinta, _quemado * 1.6f);
			DibujarTablon(new Vector2(0f, -Alto * 0.32f), 0.5f, madera);
			DibujarTablon(new Vector2(0f, -Alto * 0.32f), -0.5f, madera);
			DibujarTablon(new Vector2(0f, -Alto * 0.7f), 0.1f, madera);
		}

		// Llamas mientras se quema.
		if (_quemandose)
		{
			float intensidad = Mathf.Sin(Mathf.Min(_quemado, 1f) * Mathf.Pi) + 0.3f;
			for (int i = 0; i < 7; i++)
			{
				float x = -Ancho * 0.45f + i * Ancho * 0.15f;
				float baseY = -alturaVisible + 10f;
				float alto = (50f + 28f * Mathf.Sin(_t * 15f + i * 1.9f)) * intensidad;
				DibujarLlama(new Vector2(x, baseY), alto, 18f);
			}
			DrawCircle(new Vector2(0f, -alturaVisible), 90f * intensidad, new Color(ColorFuego, 0.15f));
		}

		// Brasas.
		foreach (Vector3 b in _brasas)
		{
			DrawCircle(new Vector2(b.X, b.Y), 5f * b.Z, new Color(ColorFuego, 0.25f * b.Z));
			DrawCircle(new Vector2(b.X, b.Y), 2f, new Color(Amarillo, b.Z));
		}

		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);

		// Aviso de qué hacer (o la pista tras golpearla con el arcano).
		float alfa = Mathf.Max(_alfaAviso, _alfaPista);
		string texto = _alfaPista > _alfaAviso ? Pista : _aviso;
		if (alfa > 0f && texto != "")
		{
			int tam = 22;
			Vector2 medida = _fuente.GetStringSize(texto, HorizontalAlignment.Left, -1, tam);
			var pos = new Vector2(-medida.X * 0.5f, -Alto - 36f + Mathf.Sin(_t * 3f) * 3f);
			Color colorAviso = _jugadorTieneFuego ? new Color("FFC247") : BlancoHueso;
			DrawStringOutline(_fuente, pos, texto, HorizontalAlignment.Left, -1, tam, 8, new Color(0f, 0f, 0f, alfa));
			DrawString(_fuente, pos, texto, HorizontalAlignment.Left, -1, tam, new Color(colorAviso, alfa));
		}
	}

	private void DibujarTablon(Vector2 centro, float angulo, Color color)
	{
		DrawSetTransform(centro + new Vector2(Mathf.Sin(_t * 60f) * 4f * _temblor, 0f), angulo, Vector2.One);
		var rect = new Rect2(-Ancho * 0.6f, -9f, Ancho * 1.2f, 18f);
		DrawRect(rect, color);
		DrawRect(rect, Tinta, false, 3f);
		DrawCircle(new Vector2(-Ancho * 0.5f, 0f), 3f, Tinta);
		DrawCircle(new Vector2(Ancho * 0.5f, 0f), 3f, Tinta);
		DrawSetTransform(new Vector2(Mathf.Sin(_t * 60f) * 4f * _temblor, 0f), 0f, Vector2.One);
	}

	private void DibujarLlama(Vector2 base_, float alto, float ancho)
	{
		foreach (var (color, escala) in new[] { (Rojo, 1f), (ColorFuego, 0.72f), (Amarillo, 0.42f) })
		{
			float a = ancho * escala;
			float h = alto * escala;
			DrawColoredPolygon(new[]
			{
				base_ + new Vector2(-a, 0f),
				base_ + new Vector2(-a * 0.4f, -h * 0.6f),
				base_ + new Vector2(0f, -h),
				base_ + new Vector2(a * 0.4f, -h * 0.6f),
				base_ + new Vector2(a, 0f),
			}, color);
		}
	}
}
