﻿// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using System.Reflection;
using SkiaSharp;
using Svg.Document_Structure;

namespace Svg.Skia
{
    public class UseDrawable : Drawable
    {
        public Drawable? ReferencedDrawable;

        public UseDrawable(SvgUse svgUse, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            IgnoreDisplay = ignoreDisplay;
            IsDrawable = CanDraw(svgUse, IgnoreDisplay);

            if (!IsDrawable)
            {
                return;
            }

            if (SvgExtensions.HasRecursiveReference(svgUse, (e) => e.ReferencedElement, new HashSet<Uri>()))
            {
                IsDrawable = false;
                return;
            }

            var svgReferencedElement = SvgExtensions.GetReference<SvgVisualElement>(svgUse, svgUse.ReferencedElement);
            if (svgReferencedElement == null)
            {
                IsDrawable = false;
                return;
            }

            float x = svgUse.X.ToDeviceValue(UnitRenderingType.Horizontal, svgUse, skOwnerBounds);
            float y = svgUse.Y.ToDeviceValue(UnitRenderingType.Vertical, svgUse, skOwnerBounds);
            float width = svgUse.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgUse, skOwnerBounds);
            float height = svgUse.Height.ToDeviceValue(UnitRenderingType.Vertical, svgUse, skOwnerBounds);

            if (width <= 0f)
            {
                width = new SvgUnit(SvgUnitType.Percentage, 100f).ToDeviceValue(UnitRenderingType.Horizontal, svgUse, skOwnerBounds);
            }

            if (height <= 0f)
            {
                height = new SvgUnit(SvgUnitType.Percentage, 100f).ToDeviceValue(UnitRenderingType.Vertical, svgUse, skOwnerBounds);
            }

            var originalParent = svgUse.Parent;
            var useParent = svgUse.GetType().GetField("_parent", BindingFlags.NonPublic | BindingFlags.Instance);
            if (useParent != null)
            {
                useParent.SetValue(svgReferencedElement, svgUse);
            }

            svgReferencedElement.InvalidateChildPaths();

            if (svgReferencedElement is SvgSymbol svgSymbol)
            {
                ReferencedDrawable = new SymbolDrawable(svgSymbol, x, y, width, height, skOwnerBounds, ignoreDisplay);
                _disposable.Add(ReferencedDrawable);
            }
            else
            {
                var drawable = DrawableFactory.Create(svgReferencedElement, skOwnerBounds, ignoreDisplay);
                if (drawable != null)
                {
                    ReferencedDrawable = drawable;
                    _disposable.Add(ReferencedDrawable);
                }
                else
                {
                    IsDrawable = false;
                    return;
                }
            }

            IsAntialias = SKPaintUtil.IsAntialias(svgUse);

            TransformedBounds = ReferencedDrawable.TransformedBounds;

            Transform = SKMatrixUtil.GetSKMatrix(svgUse.Transforms);
            if (!(svgReferencedElement is SvgSymbol))
            {
                var skMatrixTranslateXY = SKMatrix.MakeTranslation(x, y);
                SKMatrix.PreConcat(ref Transform, ref skMatrixTranslateXY);
            }

            PathClip = SvgClipPathUtil.GetSvgVisualElementClipPath(svgUse, TransformedBounds, new HashSet<Uri>(), _disposable);
            PaintOpacity = SKPaintUtil.GetOpacitySKPaint(svgUse, _disposable);
            PaintFilter = SKPaintUtil.GetFilterSKPaint(svgUse, _disposable);

            PaintFill = null;
            PaintStroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);

            if (useParent != null)
            {
                useParent.SetValue(svgReferencedElement, originalParent);
            }
        }

        protected override void OnDraw(SKCanvas canvas)
        {
            if (!IsDrawable)
            {
                return;
            }

            canvas.Save();

            if (ClipRect != null)
            {
                canvas.ClipRect(ClipRect.Value, SKClipOperation.Intersect);
            }

            var skMatrixTotal = canvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref Transform);
            canvas.SetMatrix(skMatrixTotal);

            if (PathClip != null && !PathClip.IsEmpty)
            {
                canvas.ClipPath(PathClip, SKClipOperation.Intersect, IsAntialias);
            }

            if (PaintOpacity != null)
            {
                canvas.SaveLayer(PaintOpacity);
            }

            if (PaintFilter != null)
            {
                canvas.SaveLayer(PaintFilter);
            }

            ReferencedDrawable?.Draw(canvas, 0f, 0f);

            if (PaintFilter != null)
            {
                canvas.Restore();
            }

            if (PaintOpacity != null)
            {
                canvas.Restore();
            }

            canvas.Restore();
        }

        public override Drawable? HitTest(SKPoint skPoint)
        {
            var result = ReferencedDrawable?.HitTest(skPoint);
            if (result != null)
            {
                return result;
            }
            return base.HitTest(skPoint);
        }
    }
}
