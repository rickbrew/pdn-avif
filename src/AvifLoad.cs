////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020-2026 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using AvifFileType.AvifContainer;
using AvifFileType.Exif;
using PaintDotNet.FileTypes;
using PaintDotNet.Imaging;
using System;
using System.IO;

namespace AvifFileType
{
    internal static class AvifLoad
    {
        public static IFileTypeDocument Load(IFileTypeDocumentFactory factory, Stream input, IImagingFactory imagingFactory)
        {
            using AvifReader reader = new AvifReader(input, leaveOpen: true, imagingFactory);
            using AvifReaderImage image = reader.Decode();

            // Dispatch from pixel format enum to the pixel format's Color struct type
            PixelFormat pixelFormat = image.WICPixelFormat;

            if (pixelFormat == PixelFormats.Bgra32)
            {
                return image.IsPremultipliedAlpha
                    ? Load<ColorPbgra32>(factory, reader, image, imagingFactory)
                    : Load<ColorBgra32>(factory, reader, image, imagingFactory);
            }
            else if (pixelFormat == PixelFormats.Rgba64)
            {
                return image.IsPremultipliedAlpha
                    ? Load<ColorPrgba64>(factory, reader, image, imagingFactory)
                    : Load<ColorRgba64>(factory, reader, image, imagingFactory);
            }
            else if (pixelFormat == PixelFormats.Rgba128Float)
            {
                return image.IsPremultipliedAlpha
                    ? Load<ColorPrgba128Float>(factory, reader, image, imagingFactory)
                    : Load<ColorRgba128Float>(factory, reader, image, imagingFactory);
            }
            else
            {
                ExceptionUtil.UnsupportedPixelFormat(pixelFormat);
                return null!; // Unreachable
            }
        }

        private static IFileTypeDocument Load<TPixel>(IFileTypeDocumentFactory factory,
                                                      AvifReader reader,
                                                      AvifReaderImage image,
                                                      IImagingFactory imagingFactory)
                                                      where TPixel : unmanaged, INaturalPixelInfo
        {
            using IBitmap<TPixel> imageSource = image.Image.Cast<TPixel>();
            IFileTypeDocument<TPixel> doc = factory.CreateDocument<TPixel>(image.Size);

            AddAvifMetadataToDocument(doc, reader, image, imagingFactory);

            using IFileTypeBitmapLayer<TPixel> layer = doc.CreateBitmapLayer();
            using IFileTypeBitmapSink<TPixel> layerBitmapSink = layer.GetBitmap();

            PixelFormatNumericRepresentation formatRepresentation = default(TPixel).NumericRepresentation;
            CicpColorSpace cicp = image.CICPColor;

            IColorContext? colorContext = null;
            IBitmapSource<TPixel> layerSource;

            if (formatRepresentation == PixelFormatNumericRepresentation.Float)
            {
                // Floating-point formats are HDR and should use linear gamma
                if (image.HDRFormat != HDRFormat.PQ && image.HDRFormat != HDRFormat.HLG)
                {
                    throw new FormatException($"Unsupported HDR format for PixelFormat.{default(TPixel).PixelFormat.GetName()}: {image.HDRFormat}.");
                }

                // If an HDR image has an ICC profile, it is an error and should just be ignored/discarded

                bool setHdrMetadata;

                if (cicp.CanCreateColorContext)
                {
                    // Prefer to use a color context crafted directly from CICP
                    IColorContext sourceColorContext = imagingFactory.CreateColorContext(cicp);
                    colorContext = imagingFactory.CreateLinearizedColorContextOrScRgb(sourceColorContext);
                    layerSource = imageSource.CreateColorTransformer(sourceColorContext, colorContext);
                    setHdrMetadata = true;
                }
                else if (cicp.CanColorTransformFrom)
                {
                    // Transform to the linearized version of the color space that PDN recommends
                    IColorContext sourceColorContext = imagingFactory.CreateColorContext(cicp.RecommendedColorSpace);
                    colorContext = imagingFactory.CreateLinearizedColorContextOrScRgb(sourceColorContext);
                    layerSource = imageSource.CreateColorTransformer(cicp, colorContext);
                    setHdrMetadata = true;
                }
                else
                {
                    // As a last resort, use sRGB and skip the color transform
                    // This will almost certainly look wrong, but the alternative is to throw an exception and refuse to load the image
                    colorContext = imagingFactory.CreateColorContext(KnownColorSpace.Srgb);
                    layerSource = imageSource;
                    setHdrMetadata = false;
                }

                if (setHdrMetadata)
                {
                    ContentLightLevelInformationBox? clliBox = reader.GetContentLightLevelInformationBox();
                    MasteringDisplayColourVolumeBox? mdcvBox = reader.GetMasteringDisplayColourVolumeBox();

                    float maxCLL = clliBox?.MaxContentLightLevel ?? 0;
                    float masteringMaxNits = (float)(mdcvBox?.MaxDisplayMasteringNits ?? 0);
                    float? contentMaxLuminanceNits =
                        maxCLL > 0 ? maxCLL :
                        masteringMaxNits > 0 ? masteringMaxNits :
                        (cicp.TransferCharacteristics == CicpTransferCharacteristics.AribStdB67Hlg) ? 1000 // HLG should always use 1000 instead of allowing PDN to auto-measure
                        : null;

                    using (IFileTypeHdrMetadataTransaction hdrTx = doc.Metadata.Hdr.CreateTransaction())
                    {
                        hdrTx.IsHdrDocument = true;
                        hdrTx.ContentMaxLuminanceNits = contentMaxLuminanceNits;
                    }
                }
            }
            else if (formatRepresentation == PixelFormatNumericRepresentation.UnsignedInteger)
            {
                // Integer pixel formats are SDR and generally shouldn't use linear gamma
                if (image.HDRFormat != HDRFormat.None)
                {
                    throw new FormatException($"Unsupported HDR format for PixelFormat.{default(TPixel).PixelFormat.GetName()}: {image.HDRFormat}.");
                }

                ReadOnlyMemory<byte> iccProfile = reader.GetICCProfile();
                if (!iccProfile.IsEmpty && IccProfileIsRgb(iccProfile.Span))
                {
                    // For SDR, the ICC profile takes precedence over CICP after libaom has used CICP for decoding to RGB
                    // https://github.com/AOMediaCodec/av1-avif/issues/84#issuecomment-626480194
                    colorContext = imagingFactory.CreateColorContext(iccProfile.Span);
                    layerSource = imageSource;
                }
                else if (cicp.CanCreateColorContext)
                {
                    // Prefer to use a color context crafted directly from CICP
                    colorContext = imagingFactory.CreateColorContext(cicp);
                    layerSource = imageSource;
                }
                else if (cicp.CanColorTransformFrom)
                {
                    // Transform to the color space that PDN recommends
                    colorContext = imagingFactory.CreateColorContext(cicp.RecommendedColorSpace);
                    layerSource = imageSource.CreateColorTransformer(cicp, colorContext);
                }
                else
                {
                    // As a last resort, use sRGB and skip the color transform
                    colorContext = imagingFactory.CreateColorContext(KnownColorSpace.Srgb);
                    layerSource = imageSource;
                }
            }
            else
            {
                // Fixed-point, signed integer, and indexed are not supported
                throw new FormatException($"PixelFormat.{default(TPixel).PixelFormat.GetName()} is unsupported because of its numeric representation: {formatRepresentation}");
            }

            layerBitmapSink.WriteSource(layerSource);
            doc.Layers.Add(layer);
            doc.SetColorContext(colorContext);

            return doc;

            static bool IccProfileIsRgb(ReadOnlySpan<byte> iccProfile)
            {
                ICCProfile.ProfileHeader header = new ICCProfile.ProfileHeader(iccProfile);

                return header.ColorSpace == ICCProfile.ProfileColorSpace.Rgb;
            }
        }

        private static void AddAvifMetadataToDocument(IFileTypeDocument doc,
                                                      AvifReader reader,
                                                      AvifReaderImage image,
                                                      IImagingFactory imagingFactory)
        {
            AvifItemData? exif = reader.GetExifData();

            if (exif != null)
            {
                try
                {
                    ExifValueCollection? exifValues = ExifParser.Parse(exif);

                    if (exifValues != null)
                    {
                        // ICC profile will be added via SetColorContext
                        exifValues.Remove(ExifPropertyKeys.Image.InterColorProfile.Path);

                        // The HEIF specification states that the EXIF orientation tag is only
                        // informational and should not be used to rotate the image.
                        // See https://github.com/strukturag/libheif/issues/227#issuecomment-642165942
                        exifValues.Remove(ExifPropertyKeys.Image.Orientation.Path);

                        using (IFileTypeExifMetadataTransaction exifTx = doc.Metadata.Exif.CreateTransaction())
                        {
                            exifTx.SetItems(exifValues);
                        }
                    }
                }
                finally
                {
                    exif.Dispose();
                }
            }

            ImageGridMetadata? imageGridMetadata = reader.ImageGridMetadata;

            if (imageGridMetadata != null)
            {
                using IFileTypeCustomMetadataTransaction customTx = doc.Metadata.Custom.CreateTransaction();
                imageGridMetadata.SerializeToPropertyBag(customTx);
            }

            AvifItemData? xmp = reader.GetXmpData();

            if (xmp != null)
            {
                try
                {
                    using (Stream stream = xmp.GetStream())
                    {
                        XmpPacket? xmpPacket = XmpPacket.TryParse(stream);
                        using IFileTypeXmpMetadataTransaction xmpTx = doc.Metadata.Xmp.CreateTransaction();
                        xmpTx.XmpPacket = xmpPacket;
                    }
                }
                finally
                {
                    xmp.Dispose();
                }
            }
        }
    }
}
