using Android.App;
using Android.Widget;
using Android.OS;
using Android.Views;
using Android.Graphics;
using Android.Content;
using Android.Util;
using System.Collections.Generic;
using Android.Graphics.Drawables;
using Java.Interop;


// Free textures: http://shizoo-design.de/patterns.php?page=2

namespace BoubouDraw
{
	[Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Landscape, Label = "BoubouDraw", MainLauncher = true, Icon = "@mipmap/icon")]
	public class MainActivity : Activity
	{
		DrawingView DrawingView;

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			SetContentView(Resource.Layout.Main);
			DrawingView = FindViewById<DrawingView>(Resource.Id.drawView);
		}

		[Export("SetPaintColor")]
		public void SetPaintColor(View v)
		{
			var b = (Button)v;
			var buttonColor = (ColorDrawable)b.Background;
			var color = buttonColor.Color;
			DrawingView.SetPaint(color);
		}

		[Export("SetPaintTexture")]
		public void SetPaintTexture(View v)
		{
			var imageButton = v as ImageButton;
			var textureName = imageButton.Tag.ToString();
			DrawingView.SetPaint(textureName);
		}

		[Export("Save")]
		public void Save(View v)
		{
			DrawingView.Save();
		}

		[Export("Undo")]
		public void Undo(View v)
		{
			DrawingView.Undo();
		}

		[Export("SetShape")]
		public void SetShape(View v)
		{
			var imageButton = v as ImageButton;
			var chosenShape = imageButton.Tag.ToString();
			ShapeKind shapeKind = ShapeKind.FreeStyle;

			if (chosenShape == "circle")
			{
				shapeKind = ShapeKind.Circle;
			}

			if (chosenShape == "line")
			{
				shapeKind = ShapeKind.Line;
			}

			if (chosenShape == "rectangle")
			{
				shapeKind = ShapeKind.Rectangle;
			}

			DrawingView.SetShape(shapeKind);
		}
	}

	public class DrawingView : SurfaceView
	{
		public Color BackgroundColor { get; set; } = Color.White;

		private Activity Activity;

		public DrawingView(Context context) : base(context)
		{
			Initialize(context);
		}

		public DrawingView(Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle)
		{
			Initialize(context);
		}

		public DrawingView(Context context, IAttributeSet attrs) : base(context, attrs)
		{
			Initialize(context);
		}

		void Initialize(Context context)
		{
			Activity = context as Activity;
			SurfaceHolder = Holder;
		}

		private ISurfaceHolder SurfaceHolder;

		private string CurrentTexture;

		private Color CurrentColor { get; set; } = Color.Pink;

		private float CurrentSize = 8f;

		private ShapeKind CurrentShape { get; set; } = ShapeKind.FreeStyle;

		private Dictionary<string, Paint> Paints = new Dictionary<string, Paint>();

		public Paint CurrentPaint { get; set; }

		private Paint GetPaint(Color color, float size)
		{
			var key = color.ToArgb().ToString().PadLeft(20, '0') + size.ToString().PadLeft(8, '0');

			if (!Paints.ContainsKey(key))
			{
				var paint = new Paint(PaintFlags.AntiAlias);
				paint.Color = color;
				paint.StrokeWidth = size;
				paint.SetStyle(Paint.Style.Fill);
				Paints.Add(key, paint);
			}

			return Paints[key];
		}

		public Paint GetPaint(string textureName, float strokeWidth)
		{
			var id = Activity.Resources.GetIdentifier(textureName, "drawable", Activity.PackageName);
			var bm = BitmapFactory.DecodeResource(Activity.Resources, id);
			var size = new PointF(bm.Width, bm.Height);
			var key = textureName.ToString().PadLeft(20, '0') + size.ToString().PadLeft(8, '0');

			if (!Paints.ContainsKey(key))
			{
				var paint = new Paint(PaintFlags.AntiAlias);
				paint.Color = Color.White;
				paint.SetShader(new BitmapShader(bm, Shader.TileMode.Repeat, Shader.TileMode.Repeat));
				paint.StrokeWidth = strokeWidth;
				paint.SetStyle(Paint.Style.Fill);
				Paints.Add(key, paint);
			}

			return Paints[key];
		}

		public void SetShape(ShapeKind shapeKind)
		{
			CurrentShape = shapeKind;
		}

		public void SetPaint(Color color)
		{
			CurrentPaint = GetPaint(color, CurrentSize);
			CurrentColor = color;
			CurrentTexture = "";
		}

		public void SetPaint(string textureName)
		{
			CurrentPaint = GetPaint(textureName, CurrentSize);
			CurrentTexture = textureName;
		}

		public void Undo()
		{
			if (Shapes.Count > 0)
			{
				Shapes.RemoveAt(Shapes.Count - 1);
				var canvas = SurfaceHolder.LockCanvas();
				ShowShapes(canvas);
				SurfaceHolder.UnlockCanvasAndPost(canvas);
			}
		}
		
		public void Save()
		{
			if (Shapes.Count == 0) return;
			var bitmap = Bitmap.CreateBitmap(this.Width, this.Height, Bitmap.Config.Argb8888);
			var canvas = new Canvas(bitmap);
			ShowShapes(canvas);
			var stream = new System.IO.MemoryStream();
		    bitmap.Compress(Bitmap.CompressFormat.Png, 100, stream); // bmp is your Bitmap instance
		    var fileName = string.Format("draw-{0:yyyy-MM-dd_hh-mm-ss-tt}.png", System.DateTime.Now);
			var location = Android.OS.Environment.ExternalStorageDirectory.AbsolutePath;
			System.IO.File.WriteAllBytes(System.IO.Path.Combine(location, fileName), stream.ToArray());
			Toast.MakeText(Activity, "Saved", ToastLength.Short);
			bitmap.Dispose();
		}

		private PointF _start;

		public override bool OnTouchEvent(Android.Views.MotionEvent e)
		{
			var pos = new PointF(e.GetX(), e.GetY());

			if (SurfaceHolder.Surface.IsValid)
			{

				var canvas = SurfaceHolder.LockCanvas();
				ShowShapes(canvas);
				PreviewAndAddShape(canvas, pos, e);
				SurfaceHolder.UnlockCanvasAndPost(canvas);
			}

			return true;
		}

		private void ShowShapes(Canvas canvas)
		{
			canvas.DrawColor(BackgroundColor);
			for (int i = 0; i < Shapes.Count; i++)
			{
				DrawShape(canvas, Shapes[i]);
			}
		}

		private PointF _linkPreviousPos;
		private void PreviewAndAddShape(Canvas canvas, PointF pos, Android.Views.MotionEvent e)
		{
			var linkToPrevious = false;

			if (e.Action == MotionEventActions.Down)
			{
				_start = pos;
			}

			linkToPrevious = (e.Action == MotionEventActions.Move || e.Action == MotionEventActions.Up) && (CurrentShape == ShapeKind.FreeStyle);

			var paint = !string.IsNullOrEmpty(CurrentTexture)
				? GetPaint(CurrentTexture, CurrentSize)
				: GetPaint(CurrentColor, CurrentSize);

			var shape = new Shape(linkToPrevious ? _linkPreviousPos : _start, pos, CurrentColor, CurrentTexture, CurrentSize, CurrentShape, paint);
			shape.LinkToPrevious = linkToPrevious;

			DrawShape(canvas, shape);

			if (CurrentShape == ShapeKind.FreeStyle || e.Action == MotionEventActions.Up)
			{
				Shapes.Add(shape);
			}

			_linkPreviousPos = pos;
		}

		private void DrawShape(Canvas canvas, Shape shape)
		{
			switch (shape.ShapeKind)
			{
				case ShapeKind.FreeStyle:
					if (shape.LinkToPrevious)
					{
						canvas.DrawLine(shape.Start.X, shape.Start.Y, shape.End.X, shape.End.Y, shape.Paint);
					}
					else
					{
						canvas.DrawPoint(shape.End.X, shape.End.Y, shape.Paint);
					}
					break;
				case ShapeKind.Circle:
					var radius = (shape.End.X - shape.Start.X) / 2;
					var x = shape.Start.X + radius;
					var y = shape.Start.Y + radius;
					canvas.DrawCircle(x, y, radius, shape.Paint);
					break;
				case ShapeKind.Line:
					canvas.DrawLine(shape.Start.X, shape.Start.Y, shape.End.X, shape.End.Y, shape.Paint);
					break;
				case ShapeKind.Rectangle:
					canvas.DrawRect(shape.Start.X, shape.Start.Y, shape.End.X, shape.End.Y, shape.Paint);
					break;
				default:
					break;
			}
		}

		public List<Shape> Shapes = new List<Shape>();

		public class Shape
		{
			public Shape(PointF start, PointF end, Color color, string textureName, float size, ShapeKind shapeKind, Paint paint)
			{
				Start = start;
				End = end;
				Color = color;
				TextureName = textureName;
				Paint = paint;
				Size = size;
				ShapeKind = shapeKind;
			}

			public PointF Start { get; set; }

			public PointF End { get; set; }

			public float Size { get; set; }

			public Color Color { get; set; }

			public string TextureName { get; set; }

			public Paint Paint { get; set; }

			public ShapeKind ShapeKind { get; set; }

			public bool LinkToPrevious { get; set; }

			public override bool Equals(object obj)
			{
				var shape = (Shape)obj;
				var isEquals = shape.Color == this.Color;
				isEquals = isEquals && (shape.Start.X == this.Start.X);
				isEquals = isEquals && (shape.Start.Y == this.Start.Y);
				isEquals = isEquals && (shape.End.X == this.End.X);
				isEquals = isEquals && (shape.End.Y == this.End.Y);
				isEquals = isEquals && (shape.ShapeKind == this.ShapeKind);
				isEquals = isEquals && (shape.Size == this.Size);
				isEquals = isEquals && (shape.TextureName == this.TextureName);
				return isEquals;
			}

			public override int GetHashCode()
			{
				return base.GetHashCode();
			}
		}
	}

	public enum ShapeKind
	{
		FreeStyle,
		Circle,
		Line,
		Rectangle
	}
}


