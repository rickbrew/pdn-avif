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

using PaintDotNet.Rendering;

namespace AvifFileType.AvifContainer
{
    internal sealed class MasteringDisplayColourVolumeBox
        : ItemProperty
    {
        // units of 0.00002
        private readonly ushort displayPrimariesGX;
        private readonly ushort displayPrimariesGY;
        private readonly ushort displayPrimariesBX;
        private readonly ushort displayPrimariesBY;
        private readonly ushort displayPrimariesRX;
        private readonly ushort displayPrimariesRY;

        // units of 0.00002
        private readonly ushort whitePointX;
        private readonly ushort whitePointY;

        // units of 0.0001 cd/m^2
        private readonly uint maxDisplayMasteringLuminance;
        private readonly uint minDisplayMasteringLuminance;

        /// <summary>
        /// Initializes a new instance of the <see cref="MasteringDisplayColourVolumeBox"/> class.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="header">The header.</param>
        public MasteringDisplayColourVolumeBox(in EndianBinaryReaderSegment reader, Box header)
            : base(header)
        {
            this.displayPrimariesGX = reader.ReadUInt16();
            this.displayPrimariesGY = reader.ReadUInt16();
            this.displayPrimariesBX = reader.ReadUInt16();
            this.displayPrimariesBY = reader.ReadUInt16();
            this.displayPrimariesRX = reader.ReadUInt16();
            this.displayPrimariesRY = reader.ReadUInt16();
            this.whitePointX = reader.ReadUInt16();
            this.whitePointY = reader.ReadUInt16();
            this.maxDisplayMasteringLuminance = reader.ReadUInt32();
            this.minDisplayMasteringLuminance = reader.ReadUInt32();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MasteringDisplayColourVolumeBox"/> class.
        /// </summary>
        public MasteringDisplayColourVolumeBox(
            ushort displayPrimariesGX,
            ushort displayPrimariesGY,
            ushort displayPrimariesBX,
            ushort displayPrimariesBY,
            ushort displayPrimariesRX,
            ushort displayPrimariesRY,
            ushort whitePointX,
            ushort whitePointY,
            uint maxDisplayMasteringLuminance,
            uint minDisplayMasteringLuminance)
            : base(BoxTypes.MasteringDisplayColourVolume)
        {
            this.displayPrimariesGX = displayPrimariesGX;
            this.displayPrimariesGY = displayPrimariesGY;
            this.displayPrimariesBX = displayPrimariesBX;
            this.displayPrimariesBY = displayPrimariesBY;
            this.displayPrimariesRX = displayPrimariesRX;
            this.displayPrimariesRY = displayPrimariesRY;
            this.whitePointX = whitePointX;
            this.whitePointY = whitePointY;
            this.maxDisplayMasteringLuminance = maxDisplayMasteringLuminance;
            this.minDisplayMasteringLuminance = minDisplayMasteringLuminance;
        }

        public Vector2Double DisplayPrimariesG
        {
            get => new Vector2Double(this.displayPrimariesGX / 50000.0, this.displayPrimariesGY / 50000.0);
        }

        public Vector2Double DisplayPrimariesB
        {
            get => new Vector2Double(this.displayPrimariesBX / 50000.0, this.displayPrimariesBY / 50000.0);
        }

        public Vector2Double DisplayPrimariesR
        {
            get => new Vector2Double(this.displayPrimariesRX / 50000.0, this.displayPrimariesRY / 50000.0);
        }

        public Vector2Double WhitePoint
        {
            get => new Vector2Double(this.whitePointX / 50000.0, this.whitePointY / 50000.0);
        }

        public double MaxDisplayMasteringNits
        {
            get => this.maxDisplayMasteringLuminance / 10000.0;
        }

        public double MinDisplayMasteringNits
        {
            get => this.minDisplayMasteringLuminance / 10000.0;
        }
    }
}
