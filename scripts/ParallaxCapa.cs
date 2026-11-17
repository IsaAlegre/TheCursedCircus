using Godot;
using System.Collections.Generic;

/// <summary>
/// ParallaxCapa: mueve una capa de fondo (backdrop) como un porcentaje del
/// desplazamiento de la cámara, para simular profundidad en un nivel 2D.
///
/// FactorParallax = qué porcentaje del desplazamiento de la cámara recorre la capa en pantalla:
///   0.1 → capa muy lejana: casi no se mueve (cielo, carpa del fondo).
///   0.5 → capa media.
///   0.8 → capa cercana: se mueve casi como el piso (niebla).
///   1.0 → se mueve igual que el gameplay (sin parallax).
///
/// Si AnchoRepeticion es mayor a 0, la capa se repite a los costados y se recoloca sola
/// cuando la cámara avanza, así el fondo nunca se termina.
///
/// Equivalente en Godot del script de Parallax de Unity: en Unity se guarda la posición
/// inicial y cada frame se suma "posición de la cámara × factor"; acá se hace lo mismo
/// tomando el borde izquierdo de la pantalla que muestra la cámara.
/// </summary>
public partial class ParallaxCapa : Node2D
{
	/// <summary>Porcentaje del desplazamiento de la cámara (menor = más lejos).</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float FactorParallax = 0.5f;
	/// <summary>Ancho de la imagen en píxeles para repetirla. 0 = no se repite.</summary>
	[Export] public float AnchoRepeticion = 0f;
	/// <summary>Aplicar el parallax también en vertical (si la cámara sube y baja).</summary>
	[Export] public bool SeguirVertical = false;

	private Vector2 _posicionInicial;
	private Camera2D _camara;

	public override void _Ready()
	{
		_posicionInicial = Position;
		if (AnchoRepeticion > 0f)
			CrearCopias();
	}

	/// <summary>Duplica las imágenes de la capa a izquierda y derecha para cubrir todo el nivel.</summary>
	private void CrearCopias()
	{
		var originales = new List<Node2D>();
		foreach (Node hijo in GetChildren())
		{
			if (hijo is Node2D nodo)
				originales.Add(nodo);
		}
		foreach (Node2D original in originales)
		{
			foreach (int lado in new[] { -1, 1, 2 })
			{
				var copia = (Node2D)original.Duplicate();
				copia.Position = original.Position + new Vector2(lado * AnchoRepeticion, 0f);
				AddChild(copia);
			}
		}
	}

	public override void _Process(double delta)
	{
		if (_camara == null || !IsInstanceValid(_camara))
		{
			_camara = GetViewport().GetCamera2D();
			if (_camara == null)
				return;
		}

		// Borde superior izquierdo de lo que muestra la cámara (respeta límites y zoom).
		Vector2 mitadPantalla = GetViewportRect().Size / _camara.Zoom * 0.5f;
		Vector2 esquinaCamara = _camara.GetScreenCenterPosition() - mitadPantalla;

		// Principio clave: la capa acompaña a la cámara en (1 - factor).
		// Así, en pantalla, se desplaza solo el "factor" del movimiento de la cámara.
		float x = _posicionInicial.X + esquinaCamara.X * (1f - FactorParallax);

		// Repetición infinita: si la cámara ya pasó una imagen entera, la capa salta un ancho.
		if (AnchoRepeticion > 0f)
			x += Mathf.Floor((esquinaCamara.X - x) / AnchoRepeticion) * AnchoRepeticion;

		float y = SeguirVertical
			? _posicionInicial.Y + esquinaCamara.Y * (1f - FactorParallax)
			: _posicionInicial.Y;

		Position = new Vector2(x, y);
	}
}
