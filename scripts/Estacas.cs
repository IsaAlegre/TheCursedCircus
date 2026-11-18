using Godot;

/// <summary>
/// Estacas Oxidadas (GDD, Sección 6 – Obstáculos y hazards): barrotes de hierro en punta
/// que castigan el salto impreciso. Restan 1 punto de vida al contacto, empujan al Mago
/// hacia arriba y no se pueden bloquear.
///
/// Uso: agregar un Area2D al nivel, asignarle este script y apoyar su origen en el piso.
/// Las estacas crecen hacia arriba y se reparten a los dos lados del origen.
/// Se puede rotar el nodo para ponerlas en una pared (90°) o en el techo (180°).
/// La colisión se crea por código (el aviso amarillo del editor es normal).
/// </summary>
[Tool]
public partial class Estacas : Area2D
{
	private int _cantidad = 4;
	private float _anchoEstaca = 32f;
	private float _alto = 52f;
	private Texture2D _imagen;

	/// <summary>Cuántas estacas hay en fila.</summary>
	[Export(PropertyHint.Range, "1,40")]
	public int Cantidad { get => _cantidad; set { _cantidad = Mathf.Max(1, value); QueueRedraw(); } }

	/// <summary>Ancho de cada estaca (64 = un tile).</summary>
	[Export]
	public float AnchoEstaca { get => _anchoEstaca; set { _anchoEstaca = Mathf.Max(8f, value); QueueRedraw(); } }

	[Export]
	public float Alto { get => _alto; set { _alto = Mathf.Max(8f, value); QueueRedraw(); } }

	[Export] public int Danio = 1;

	/// <summary>Opcional: imagen propia (se estira al ancho total). Vacía = se dibujan por código.</summary>
	[Export]
	public Texture2D Imagen { get => _imagen; set { _imagen = value; QueueRedraw(); } }

	private const int CapaJugador = 2;
	private static readonly Color Oxido = new("934130");        // Rojo Óxido (GDD)
	private static readonly Color OxidoOscuro = new("5C1D1C");  // Rojo Granate Desgastado
	private static readonly Color Marengo = new("3A3F44");      // Gris Marengo Oscuro
	private static readonly Color Tinta = new("0E0B0B");
	private static readonly Color Brillo = new("D2C4A7");

	private float AnchoTotal => Cantidad * AnchoEstaca;

	public override void _Ready()
	{
		QueueRedraw();
		if (Engine.IsEditorHint())
			return;

		CollisionLayer = 0;
		CollisionMask = 0;
		SetCollisionMaskValue(CapaJugador, true);
		Monitorable = false;

		// La zona que lastima es un poco más chica que el dibujo: rozar una punta
		// de costado no cuenta, así el castigo se siente justo.
		var forma = new RectangleShape2D { Size = new Vector2(AnchoTotal * 0.9f, Alto * 0.6f) };
		AddChild(new CollisionShape2D { Shape = forma, Position = new Vector2(0, -Alto * 0.3f) });
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint())
			return;
		// Se revisa en cada frame (no solo al entrar): si el Mago se queda encima,
		// vuelve a recibir daño cuando termina su invulnerabilidad.
		foreach (Node2D cuerpo in GetOverlappingBodies())
		{
			if (cuerpo.IsInGroup("jugador") && cuerpo.HasMethod("recibir_danio"))
				cuerpo.Call("recibir_danio", Danio, this, false);
		}
	}

	public override void _Draw()
	{
		float x0 = -AnchoTotal / 2f;
		if (Imagen != null)
		{
			DrawTextureRect(Imagen, new Rect2(x0, -Alto, AnchoTotal, Alto), false);
			return;
		}

		for (int i = 0; i < Cantidad; i++)
		{
			float a = x0 + i * AnchoEstaca + 3f;
			float b = a + AnchoEstaca - 6f;
			float m = (a + b) / 2f;
			// Alturas alternadas para que no se vea como un serrucho perfecto.
			float altoEstaca = Alto * (i % 2 == 0 ? 1f : 0.82f);
			var punta = new Vector2(m + (i % 3 - 1) * 2f, -altoEstaca);
			var izq = new Vector2(a, -6f);
			var der = new Vector2(b, -6f);

			DrawColoredPolygon(new[] { izq, punta, der }, Oxido);
			DrawColoredPolygon(new[] { new Vector2(m, -6f), punta, der }, OxidoOscuro);
			DrawPolyline(new[] { izq, punta, der }, Tinta, 2.5f);
			DrawLine(punta + new Vector2(-1f, 4f), punta + new Vector2(-3f, 12f), Brillo, 1.5f);
		}

		// Travesaño de hierro que sostiene las estacas.
		var base_ = new Rect2(x0, -9f, AnchoTotal, 9f);
		DrawRect(base_, Marengo);
		DrawRect(base_, Tinta, false, 2.5f);
		for (int i = 0; i < Cantidad; i++)
			DrawCircle(new Vector2(x0 + (i + 0.5f) * AnchoEstaca, -4.5f), 2f, Tinta);
	}
}
