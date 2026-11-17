using Godot;

/// <summary>
/// Salida del Nivel 1, detrás de la Barricada de Telones.
/// Cuando el Mago la cruza, la pantalla se oscurece y aparece "Fin del Acto I",
/// con un adelanto de la Zona 2. Con cualquier tecla (o a los pocos segundos) vuelve al título.
/// Colocar el nodo con su origen apoyado en el piso. La zona se crea por código.
/// </summary>
public partial class SalidaNivel : Area2D
{
	[Export] public float Ancho = 80f;
	[Export] public float Alto = 300f;
	[Export(PropertyHint.File, "*.tscn")] public string EscenaSiguiente = "res://scenes/titulo.tscn";
	[Export] public string Titulo = "Fin del Acto I";
	[Export] public string Subtitulo = "La Entrada del Circo ha sido purificada";
	[Export] public string Adelanto = "Más allá de los telones, algo se agita entre las jaulas...";

	private const string RutaFuenteTitulo = "res://assets/fuentes/PirataOne-Regular.ttf";
	private const string RutaFuenteTexto = "res://assets/fuentes/IMFellEnglish-Regular.ttf";

	private bool _terminado = false;
	private bool _puedeSalir = false;

	public override void _Ready()
	{
		CollisionLayer = 0;
		CollisionMask = 0;
		SetCollisionMaskValue(2, true);
		AddChild(new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(Ancho, Alto) },
			Position = new Vector2(0f, -Alto * 0.5f),
		});
		BodyEntered += AlEntrar;
	}

	private void AlEntrar(Node2D cuerpo)
	{
		if (_terminado || !cuerpo.IsInGroup("jugador"))
			return;
		_terminado = true;
		cuerpo.SetPhysicsProcess(false); // el Mago se detiene
		MostrarFinal();
	}

	private void MostrarFinal()
	{
		var capa = new CanvasLayer { Layer = 40 };
		AddChild(capa);

		var negro = new ColorRect { Color = new Color(0f, 0f, 0f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		negro.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		capa.AddChild(negro);

		Font fuenteTitulo = ResourceLoader.Exists(RutaFuenteTitulo) ? GD.Load<Font>(RutaFuenteTitulo) : null;
		Font fuenteTexto = ResourceLoader.Exists(RutaFuenteTexto) ? GD.Load<Font>(RutaFuenteTexto) : null;
		Label titulo = Etiqueta(capa, Titulo, fuenteTitulo, 72, 0.36f, new Color("F5F2EA"));
		Label subtitulo = Etiqueta(capa, Subtitulo, fuenteTexto, 24, 0.50f, new Color("3FD8EC"));
		Label adelanto = Etiqueta(capa, Adelanto, fuenteTexto, 20, 0.62f, new Color(1f, 1f, 1f, 0.7f));
		Label tecla = Etiqueta(capa, "Pulsá cualquier tecla para volver al título", fuenteTexto, 16, 0.88f, new Color(1f, 1f, 1f, 0.5f));

		var tw = CreateTween();
		tw.TweenProperty(negro, "color:a", 1f, 1.5f);
		tw.TweenProperty(titulo, "modulate:a", 1f, 1.0f);
		tw.TweenProperty(subtitulo, "modulate:a", 1f, 0.8f);
		tw.TweenInterval(0.8f);
		tw.TweenProperty(adelanto, "modulate:a", 1f, 1.2f);
		tw.TweenProperty(tecla, "modulate:a", 1f, 0.6f);
		tw.TweenCallback(Callable.From(() => _puedeSalir = true));
		tw.TweenInterval(8f);
		tw.TweenCallback(Callable.From(Salir));
	}

	public override void _UnhandledInput(InputEvent evento)
	{
		if (!_puedeSalir)
			return;
		if (evento is InputEventKey { Pressed: true, Echo: false } || evento is InputEventMouseButton { Pressed: true })
			Salir();
	}

	private void Salir()
	{
		if (!_puedeSalir)
			return;
		_puedeSalir = false;
		GetTree().ChangeSceneToFile(EscenaSiguiente);
	}

	private static Label Etiqueta(CanvasLayer capa, string texto, Font fuente, int tamanio, float altura, Color color)
	{
		var label = new Label
		{
			Text = texto,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Modulate = new Color(1f, 1f, 1f, 0f),
			AnchorLeft = 0f,
			AnchorRight = 1f,
			AnchorTop = altura,
			AnchorBottom = altura,
		};
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeFontSizeOverride("font_size", tamanio);
		if (fuente != null)
			label.AddThemeFontOverride("font", fuente);
		capa.AddChild(label);
		return label;
	}
}
