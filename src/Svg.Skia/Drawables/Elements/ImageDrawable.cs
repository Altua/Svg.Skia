﻿// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public class ImageDrawable : Drawable
    {
        public SKImage? Image;
        public FragmentDrawable? FragmentDrawable;
        public SKRect SrcRect = default;
        public SKRect DestRect = default;

        public ImageDrawable(SvgImage svgImage, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes = IgnoreAttributes.None)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgImage, IgnoreAttributes);

            if (!IsDrawable)
            {
                return;
            }

            float width = svgImage.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgImage, skOwnerBounds);
            float height = svgImage.Height.ToDeviceValue(UnitRenderingType.Vertical, svgImage, skOwnerBounds);
            float x = svgImage.Location.X.ToDeviceValue(UnitRenderingType.Horizontal, svgImage, skOwnerBounds);
            float y = svgImage.Location.Y.ToDeviceValue(UnitRenderingType.Vertical, svgImage, skOwnerBounds);
            var location = new SKPoint(x, y);

            if (width <= 0f || height <= 0f || svgImage.Href == null)
            {
                IsDrawable = false;
                return;
            }

            // TODO: Check for image recursive references.
            //if (SkiaUtil.HasRecursiveReference(svgImage, (e) => e.Href))
            //{
            //    _canDraw = false;
            //    return;
            //}

            var image = SvgImageUtil.GetImage(svgImage, svgImage.Href);
            var skImage = image as SKImage;
            var svgFragment = image as SvgFragment;
            if (skImage == null && svgFragment == null)
            {
                IsDrawable = false;
                return;
            }

            if (skImage != null)
            {
                _disposable.Add(skImage);
            }

            SrcRect = default;

            if (skImage != null)
            {
                SrcRect = SKRect.Create(0f, 0f, skImage.Width, skImage.Height);
            }

            if (svgFragment != null)
            {
                var skSize = SvgExtensions.GetDimensions(svgFragment);
                SrcRect = SKRect.Create(0f, 0f, skSize.Width, skSize.Height);
            }

            var destClip = SKRect.Create(location.X, location.Y, width, height);
            DestRect = destClip;

            var aspectRatio = svgImage.AspectRatio;
            if (aspectRatio.Align != SvgPreserveAspectRatio.none)
            {
                var fScaleX = destClip.Width / SrcRect.Width;
                var fScaleY = destClip.Height / SrcRect.Height;
                var xOffset = 0f;
                var yOffset = 0f;

                if (aspectRatio.Slice)
                {
                    fScaleX = Math.Max(fScaleX, fScaleY);
                    fScaleY = Math.Max(fScaleX, fScaleY);
                }
                else
                {
                    fScaleX = Math.Min(fScaleX, fScaleY);
                    fScaleY = Math.Min(fScaleX, fScaleY);
                }

                switch (aspectRatio.Align)
                {
                    case SvgPreserveAspectRatio.xMinYMin:
                        break;
                    case SvgPreserveAspectRatio.xMidYMin:
                        xOffset = (destClip.Width - SrcRect.Width * fScaleX) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMaxYMin:
                        xOffset = (destClip.Width - SrcRect.Width * fScaleX);
                        break;
                    case SvgPreserveAspectRatio.xMinYMid:
                        yOffset = (destClip.Height - SrcRect.Height * fScaleY) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMidYMid:
                        xOffset = (destClip.Width - SrcRect.Width * fScaleX) / 2;
                        yOffset = (destClip.Height - SrcRect.Height * fScaleY) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMaxYMid:
                        xOffset = (destClip.Width - SrcRect.Width * fScaleX);
                        yOffset = (destClip.Height - SrcRect.Height * fScaleY) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMinYMax:
                        yOffset = (destClip.Height - SrcRect.Height * fScaleY);
                        break;
                    case SvgPreserveAspectRatio.xMidYMax:
                        xOffset = (destClip.Width - SrcRect.Width * fScaleX) / 2;
                        yOffset = (destClip.Height - SrcRect.Height * fScaleY);
                        break;
                    case SvgPreserveAspectRatio.xMaxYMax:
                        xOffset = (destClip.Width - SrcRect.Width * fScaleX);
                        yOffset = (destClip.Height - SrcRect.Height * fScaleY);
                        break;
                }

                DestRect = SKRect.Create(
                    destClip.Left + xOffset, destClip.Top + yOffset,
                    SrcRect.Width * fScaleX, SrcRect.Height * fScaleY);
            }

            Clip = destClip;

            var skClipRect = SvgClipPathUtil.GetClipRect(svgImage, destClip);
            if (skClipRect != null)
            {
                Clip = skClipRect;
            }

            if (skImage != null)
            {
                Image = skImage;
            }

            if (svgFragment != null)
            {
                FragmentDrawable = new FragmentDrawable(svgFragment, skOwnerBounds, ignoreAttributes);
                _disposable.Add(FragmentDrawable);
            }

            IsAntialias = SKPaintUtil.IsAntialias(svgImage);

            if (Image != null)
            {
                TransformedBounds = DestRect;
            }

            if (FragmentDrawable != null)
            {
                //_skBounds = _fragmentDrawable._skBounds;
                TransformedBounds = DestRect;
            }

            Transform = SKMatrixUtil.GetSKMatrix(svgImage.Transforms);
            if (FragmentDrawable != null)
            {
                float dx = DestRect.Left;
                float dy = DestRect.Top;
                float sx = DestRect.Width / SrcRect.Width;
                float sy = DestRect.Height / SrcRect.Height;
                var skTranslationMatrix = SKMatrix.MakeTranslation(dx, dy);
                var skScaleMatrix = SKMatrix.MakeScale(sx, sy);
                SKMatrix.PreConcat(ref skTranslationMatrix, ref skScaleMatrix);
                SKMatrix.PreConcat(ref Transform, ref skTranslationMatrix);
            }

            ClipPath = SvgClipPathUtil.GetSvgVisualElementClipPath(svgImage, TransformedBounds, new HashSet<Uri>(), _disposable);
            MaskDrawable = SvgMaskUtil.GetSvgVisualElementMask(svgImage, TransformedBounds, new HashSet<Uri>(), _disposable);
            CreateMaskPaints();
            Opacity = ignoreAttributes.HasFlag(IgnoreAttributes.Opacity) ? null : SKPaintUtil.GetOpacitySKPaint(svgImage, _disposable);
            Filter = ignoreAttributes.HasFlag(IgnoreAttributes.Filter) ? null : SKPaintUtil.GetFilterSKPaint(svgImage, TransformedBounds, _disposable);

            Fill = null;
            Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }

        protected override void Draw(SKCanvas canvas)
        {
            if (Image != null)
            {
                canvas.DrawImage(Image, SrcRect, DestRect);
            }

            if (FragmentDrawable != null)
            {
                FragmentDrawable.Draw(canvas, 0f, 0f);
            }
        }

        public override Drawable? HitTest(SKPoint skPoint)
        {
            if (Image != null)
            {
                if (DestRect.Contains(skPoint))
                {
                    return this;
                }
            }

            if (FragmentDrawable != null)
            {
                var result = FragmentDrawable?.HitTest(skPoint);
                if (result != null)
                {
                    return result;
                }
            }

            return base.HitTest(skPoint);
        }
    }
}
