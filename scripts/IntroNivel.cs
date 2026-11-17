using Godot;

/// <summary>
/// Entrada del Nivel 1 después del prólogo (GDD: "caída libre inicial").
/// Si se viene del prólogo, el Mago aparece bien arriba, fuera de la pantalla, y cae
/// dentro del circo mientras la imagen se aclara desde negro. Al aterrizar, la cámara
/// tiembla y aparece el nombre de la zona.
///
/// Si se reinicia el nivel al morir, no se repite: la marca del prólogo se consume la primera vez.
/// Va en un nodo común (Node) al FINAL del árbol del nivel, así el Mago ya está listo.
/// </summary>
public partial class IntroNivel : Node
{
	/// <summary>Cuántos píxeles más arriba de su posición inicial aparece el Mago.</summary>
	[Export] public float AlturaCaida = 650f;
	[Export] public float DuracionFundido = 1.2f;
	[Export] public string NombreZona = "La Entrada del Circo";
	[Export] public string Subtitulo = "Zona I";
	/// <summary>Mostrar el nombre de la zona aunque no se venga del prólogo (por ejemplo, al probar con F6).</summary>
	[Export] public bool MostrarZonaSiempre = false;

	private const string RutaFuenteTitulo = "res://assets/fuentes/PirataOne-Regular.ttf";
	private const string RutaFuenteTexto = "res://assets/fuentes/IMFellEnglish-Regular.ttf";

	private CharacterBody2D _mago;
	private ColorRect _negro;
	private Label _zona;
	private Label _subtitulo;
	private bool _cayendo = false;
	private float _tiempoCayendo = 0f;

	public override void _Ready()
	{
		Window raiz = GetTree().Root;
		bool vieneDelPrologo = raiz.HasMeta("intro_caida") && (bool)raiz.GetMeta("intro_caida");
		if (vieneDelPrologo)
			raiz.RemoveMeta("intro_caida");

		CrearInterfaz();
		if (!vieneDelPrologo)
		{
			if (MostrarZonaSiempre)
				MostrarNombreZona();
			return;
		}

		_mago = GetTree().GetFirstNodeInGroup("jugador") as CharacterBody2D;
		if (_mago != null)
		{
			_mago.GlobalPosition += new Vector2(0f, -AlturaCaida);
			_mago.GetNodeOrNull<Camera2D>("Camera2D")?.CallDeferred(Camera2D.MethodName.ResetSmoothing);
			_cayendo = true;
		}

		_negro.Visible = true;
		CreateTween().TweenProperty(_negro, "color:a", 0f, DuracionFundido);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_cayendo || _mago == null || !IsInstanceValid(_mago))
			return;
		_tiempoCayendo += (float)delta;
		// Espera un instante para no confundir el primer frame con un aterrizaje.
		if (_tiempoCayendo > 0.2f && _mago.IsOnFloor())
		{
			_cayendo = false;
			SacudirCamara();
			MostrarNombreZona();
		}
	}

	private void CrearInterfaz()
	{
		var capa = new CanvasLayer { Layer = 30 };
		AddChild(capa);

		_negro = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
		_negro.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		capa.AddChild(_negro);

		Font fuenteTitulo = ResourceLoader.Exists(RutaFuenteTitulo) ? GD.Load<Font>(RutaFuenteTitulo) : null;
		Font fuenteTexto = ResourceLoader.Exists(RutaFuenteTexto) ? GD.Load<Font>(RutaFuenteTexto) : null;

		_subtitulo = CrearEtiqueta(capa, Subtitulo, fuenteTexto, 22, 0.30f);
		_zona = CrearEtiqueta(capa, NombreZona, fuenteTitulo, 54, 0.36f);
	}

	private static Label CrearEtiqueta(CanvasLayer capa, string texto, Font fuente, int tamanio, float altura)
	{
		var label = new Label
		{
			Text = texto,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Modulate = new Color(1f, 1f, 1f, 0f),
		};
		label.AddThemeColorOverride("font_color", new Color("F5F2EA"));
		label.AddThemeColorOverride("font_outline_color", new Color("151515"));
		label.AddThemeConstantOverride("outline_size", 8);
		label.AddThemeFontSizeOverride("font_size", tamanio);
		if (fuente != null)
			label.AddThemeFontOverride("font", fuente);
		label.AnchorLeft = 0f;
		label.AnchorRight = 1f;
		label.AnchorTop = altura;
		label.AnchorBottom = altura;
		capa.AddChild(label);
		return label;
	}

	/// <summary>El nombre de la zona aparece, se queda unos segundos y se desvanece.</summary>
	private void MostrarNombreZona()
	{
		var tw = CreateTween();
		tw.TweenInterval(0.3f);
		tw.TweenProperty(_subtitulo, "modulate:a", 1f, 0.8f);
		tw.Parallel().TweenProperty(_zona, "modulate:a", 1f, 0.8f);
		tw.TweenInterval(2.2f);
		tw.TweenProperty(_subtitulo, "modulate:a", 0f, 1.0f);
		tw.Parallel().TweenProperty(_zona, "modulate:a", 0f, 1.0f);
	}

	private void SacudirCamara()
	{
		Camera2D camara = _mago.GetNodeOrNull<Camera2D>("Camera2D");
		if (camara == null)
			return;
		Vector2 original = camara.Offset;
		var tw = CreateTween();
		for (int i = 0; i < 6; i++)
			tw.TweenProperty(camara, "offset", original + new Vector2((float)GD.RandRange(-8.0, 8.0), (float)GD.RandRange(-8.0, 8.0)), 0.035f);
		tw.TweenProperty(camara, "offset", original, 0.05f);
	}
}
