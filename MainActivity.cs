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
using System.Threading.Tasks;
using System.Threading;


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
		
		[Export("SetSize")]
		public void SetSize(View v)
		{
			var imageButton = v as ImageButton;
			var sizeName = imageButton.Tag.ToString();
			var size = (sizeName == "large") ? 48f :
						(sizeName == "medium") ? 16f : 8f;
						
			DrawingView.SetSize(size);
		}

		[Export("SetRandomColors")]
		public void SetRandomColors(View v)
		{
			DrawingView.SetRandomColors();
		}
		
		[Export("Clear")]
		public void Clear(View v)
		{
			DrawingView.Clear();
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
		
		[Export("Redo")]
		public void Redo(View v)
		{
			DrawingView.Redo();
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
			Task.Run(() =>
			{
				while (!SurfaceHolder.Surface.IsValid)
				{
					Thread.Sleep(200);
				}

				var canvas = SurfaceHolder.LockCanvas();
				canvas.DrawColor(BackgroundColor);
				SurfaceHolder.UnlockCanvasAndPost(canvas);
			});
		}

		private ISurfaceHolder SurfaceHolder;

		private string CurrentTexture;

		private Color CurrentColor { get; set; } = Color.Pink;

		private float CurrentSize = 24f;

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
				paint.StrokeCap = Paint.Cap.Round;
    			paint.StrokeJoin = Paint.Join.Round;
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
				paint.StrokeCap = Paint.Cap.Round;
    			paint.StrokeJoin = Paint.Join.Round;
				Paints.Add(key, paint);
			}

			return Paints[key];
		}

		public void SetShape(ShapeKind shapeKind)
		{
			CurrentShape = shapeKind;
		}

		private bool randomizeColor = false;
		public void SetPaint(Color color)
		{
			randomizeColor = false;
			CurrentPaint = GetPaint(color, CurrentSize);
			CurrentColor = color;
			CurrentTexture = "";
		}
		
		public void SetRandomColors()
		{
			randomizeColor = true;
		}
		
		public void Clear()
		{
			var shape = new Shape(new PointF(0, 0), new PointF(this.Width, this.Height), BackgroundColor, "", 0, ShapeKind.Rectangle, GetPaint(BackgroundColor, 0));
			Shapes.Add(shape);
			if (SurfaceHolder.Surface.IsValid)
			{
				var canvas = SurfaceHolder.LockCanvas();
				ShowShapes(canvas);
				SurfaceHolder.UnlockCanvasAndPost(canvas);
			}
		}

		public void SetPaint(string textureName)
		{
			randomizeColor = false;
			CurrentPaint = GetPaint(textureName, CurrentSize);
			CurrentTexture = textureName;
		}

		public void SetSize(float size)
		{
			CurrentSize = size;
			CurrentPaint = !string.IsNullOrEmpty(CurrentTexture) ?
				GetPaint(CurrentTexture, CurrentSize) :
				GetPaint(CurrentColor, CurrentSize);
		}
		
		public void Undo()
		{
			if (Shapes.Count > 0)
			{
				ShapeGarbage.Push(Shapes[Shapes.Count - 1]);
				Shapes.RemoveAt(Shapes.Count - 1);
				var canvas = SurfaceHolder.LockCanvas();
				ShowShapes(canvas);
				SurfaceHolder.UnlockCanvasAndPost(canvas);
			}
		}
		
		public void Redo()
		{
			if (ShapeGarbage.Count > 0)
			{
				Shapes.Add(ShapeGarbage.Pop());
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
			var toast = Toast.MakeText(Activity, "Saved", ToastLength.Short);
			toast.Show();
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
				if (randomizeColor)
				{
					CurrentColor = GetRandomColor();
				}
				
				Shapes.Add(shape);
			}

			_linkPreviousPos = pos;
		}


		private System.Random random = new System.Random();
		private Color GetRandomColor()
		{
			var rnd = random.Next(16);
			var color = Activity.Resources.GetColor(Activity.Resources.GetIdentifier("c" + rnd.ToString().PadLeft(3, '0'), "color", Activity.PackageName));
			return color;
		}
		
		private void DrawShape(Canvas canvas, Shape shape)
		{
			var startX = shape.Start.X;
			var endX = shape.End.X;
			var startY = shape.Start.Y;
			var endY = shape.End.Y;

			if (shape.ShapeKind == ShapeKind.Circle || shape.ShapeKind == ShapeKind.Rectangle)
			{
				// Allow displaying paint in negative rectangle for rectangles and ovals
				if (endX < startX)
				{
					var newStartX = endX;
					endX = startX;
					startX = newStartX;
				}
				
				if (endY < startY)
				{
					var newStartY = endY;
					endY = startY;
					startY = newStartY;
				}
			}
			
			switch (shape.ShapeKind)
			{
				case ShapeKind.FreeStyle:
					if (shape.LinkToPrevious)
					{
						canvas.DrawLine(startX, startY, endX, endY, shape.Paint);
					}
					else
					{
						canvas.DrawPoint(endX, endY, shape.Paint);
					}
					break;
				case ShapeKind.Circle:
					canvas.DrawOval(new RectF(startX, startY, endX, endY), shape.Paint);
					break;
				case ShapeKind.Line:
					canvas.DrawLine(startX, startY, endX, endY, shape.Paint);
					break;
				case ShapeKind.Rectangle:
					canvas.DrawRect(startX, startY, endX, endY, shape.Paint);
					break;
				default:
					break;
			}
		}

		public List<Shape> Shapes = new List<Shape>();
		public Stack<Shape> ShapeGarbage = new Stack<Shape>();

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


