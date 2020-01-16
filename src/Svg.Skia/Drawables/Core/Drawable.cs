﻿// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using SkiaSharp;

namespace Svg.Skia
{
    public abstract class Drawable : SKDrawable
    {
        internal CompositeDisposable _disposable = new CompositeDisposable();

        public bool IsDrawable;
        public IgnoreAttributes IgnoreAttributes;
        public bool IsAntialias;
        public SKRect TransformedBounds;
        public SKMatrix Transform;
        public SKRect? Clip;
        public SKPath? ClipPath;
        public MaskDrawable? MaskDrawable;
        public SKPaint? Mask;
        public SKPaint? MaskDstIn;
        public SKPaint? Opacity;
        public SKPaint? Filter;
        public SKPaint? Fill;
        public SKPaint? Stroke;

        protected bool CanDraw(SvgVisualElement svgVisualElement, IgnoreAttributes ignoreAttributes)
        {
            bool visible = svgVisualElement.Visible == true;
            bool display = ignoreAttributes.HasFlag(IgnoreAttributes.Display) ? true : !string.Equals(svgVisualElement.Display, "none", StringComparison.OrdinalIgnoreCase);
            return visible && display;
        }

        protected override SKRect OnGetBounds()
        {
            if (IsDrawable)
            {
                return TransformedBounds;
            }
            return SKRect.Empty;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _disposable?.Dispose();
        }

        protected void CreateMaskPaints()
        {
            if (MaskDrawable == null)
            {
                return;
            }

            Mask = new SKPaint()
            {
            };
            _disposable.Add(Mask);

            MaskDstIn = new SKPaint
            {
                BlendMode = SKBlendMode.DstIn,
                ColorFilter = SKColorFilter.CreateColorMatrix(
                    new float[]
                    {
                        0, 0, 0, 0, 0,
                        0, 0, 0, 0, 0,
                        0, 0, 0, 0, 0,
                        0.2125f, 0.7154f, 0.0721f, 0, 0
                    })
            };
            _disposable.Add(MaskDstIn);
        }

        protected abstract void Draw(SKCanvas canvas);

        protected override void OnDraw(SKCanvas canvas)
        {
            if (!IsDrawable)
            {
                return;
            }

            canvas.Save();

            if (Clip != null)
            {
                canvas.ClipRect(Clip.Value, SKClipOperation.Intersect);
            }

            var skMatrixTotal = canvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref Transform);
            canvas.SetMatrix(skMatrixTotal);

            if (ClipPath != null)
            {
                canvas.ClipPath(ClipPath, SKClipOperation.Intersect, IsAntialias);
            }

            if (MaskDrawable != null && Opacity == null)
            {
                canvas.SaveLayer(Mask);
            }

            if (Opacity != null)
            {
                canvas.SaveLayer(Opacity);
            }

            if (Filter != null)
            {
                canvas.SaveLayer(Filter);
            }

            Draw(canvas);

            if (Filter != null)
            {
                canvas.Restore();
            }

            if (Opacity != null && MaskDrawable == null)
            {
                canvas.Restore();
            }

            if (MaskDrawable != null)
            {
                canvas.SaveLayer(MaskDstIn);
                MaskDrawable.Draw(canvas, 0f, 0f);
                canvas.Restore();
                canvas.Restore();
            }

            canvas.Restore();
        }

        public virtual Drawable? HitTest(SKPoint skPoint)
        {
            if (TransformedBounds.Contains(skPoint))
            {
                return this;
            }
            return null;
        }
    }
}
