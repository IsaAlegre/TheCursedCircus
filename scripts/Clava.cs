using Godot;

/// <summary>
/// Clava que lanza el Malabarista Corrupto: vuela en arco (con gravedad) girando sobre sí misma.
/// Si golpea al Mago le quita 1 de vida. Si el Mago hace un BLOQUEO PERFECTO, la clava
/// se devuelve en línea recta hacia el Malabarista y lo daña.
/// </summary>
public partial class Clava : Area2D
{
	public Vector2 Velocidad;
	public Node2D Lanzador;
	public float Gravedad = 900f;
	public int Danio = 1;
	public int DanioDevuelta = 30;

	private static readonly Color BlancoHueso = new("F5F2EA");
	private static readonly Color Carmesi = new("9C171F");
	private static readonly Color Tinta = new("151515");
	private const float VelocidadDevuelta = 750f;

	private bool _devuelta = false;
	private bool _terminada = false;
	private float _vida = 4f;
	private float _giro = 0f;

	public override void _Ready()
	{
		CollisionLayer = 0;
		CollisionMask = 0;
		SetCollisionMaskValue(1, true); // mundo
		SetCollisionMaskValue(2, true); // jugador
		var forma = new CollisionShape2D { Shape = new CircleShape2D { Radius = 13f } };
		AddChild(forma);
		BodyEntered += AlTocar;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (_terminada)
			return;
		_vida -= dt;
		if (_vida <= 0f)
		{
			Desaparecer();
			return;
		}

		if (_devuelta && Lanzador != null && IsInstanceValid(Lanzador))
		{
			// Vuelve directo hacia el Malabarista.
			Vector2 objetivo = Lanzador.HasMethod("centro") ? Lanzador.Call("centro").AsVector2() : Lanzador.GlobalPosition;
			Velocidad = (objetivo - GlobalPosition).Normalized() * VelocidadDevuelta;
		}
		else if (!_devuelta)
		{
			Velocidad.Y += Gravedad * dt;
		}
		GlobalPosition += Velocidad * dt;
		_giro += dt * (_devuelta ? 22f : 12f);
		QueueRedraw();
	}

	private void AlTocar(Node2D cuerpo)
	{
		if (_terminada)
			return;

		if (!_devuelta && cuerpo.IsInGroup("jugador"))
		{
			// Si el Mago bloquea perfecto, llama a Aturdir() durante esta llamada y la clava se devuelve.
			cuerpo.Call("recibir_danio", Danio, this, true);
			if (!_devuelta)
				Desaparecer();
			return;
		}

		if (_devuelta && cuerpo == Lanzador)
		{
			cuerpo.Call("recibir_danio", DanioDevuelta, this);
			Desaparecer();
			return;
		}

		// Choque con el mundo (piso, paredes).
		if (cuerpo is not CharacterBody2D)
			Desaparecer();
	}

	/// <summary>Lo llama el Mago al hacer un bloqueo perfecto (igual que con los demás enemigos).</summary>
	public void aturdir()
	{
		if (_devuelta)
			return;
		_devuelta = true;
		_vida = 3f;
		// Al volver ya no le hace daño al Mago y sí al Malabarista (capa 3 = enemigos).
		SetCollisionMaskValue(2, false);
		SetCollisionMaskValue(1, false);
		SetCollisionMaskValue(3, true);
	}

	private void Desaparecer()
	{
		_terminada = true;
		SetDeferred(Area2D.PropertyName.Monitoring, false);
		var tw = CreateTween();
		tw.TweenProperty(this, "modulate:a", 0f, 0.15f);
		tw.TweenCallback(Callable.From(QueueFree));
	}

	public override void _Draw()
	{
		DrawSetTransform(Vector2.Zero, _giro, Vector2.One);
		if (_devuelta)
			DrawCircle(Vector2.Zero, 20f, new Color("8A2BE2", 0.35f));
		// Clava de malabares: cuerpo ovalado con franja carmesí y mango.
		DrawRect(new Rect2(-3f, 4f, 6f, 14f), Tinta);
		DrawRect(new Rect2(-2f, 5f, 4f, 12f), BlancoHueso);
		var cuerpo = new Vector2[12];
		for (int i = 0; i < 12; i++)
		{
			float a = Mathf.Tau * i / 12f;
			cuerpo[i] = new Vector2(Mathf.Cos(a) * 7f, Mathf.Sin(a) * 11f - 4f);
		}
		DrawColoredPolygon(cuerpo, BlancoHueso);
		DrawRect(new Rect2(-7f, -6f, 14f, 4f), Carmesi);
		var contorno = new Vector2[13];
		cuerpo.CopyTo(contorno, 0);
		contorno[12] = cuerpo[0];
		DrawPolyline(contorno, Tinta, 2f);
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}
}
