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

namespace AvifFileType.AvifContainer
{
    internal sealed class ContentLightLevelInformationBox
        : ItemProperty
    {
        private readonly ushort maxContentLightLevel; // MaxCLL, in nits
        private readonly ushort maxPicAverageLightLevel; // MaxFALL, in nits

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentLightLevelInformationBox"/> class.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="header">The header.</param>
        public ContentLightLevelInformationBox(in EndianBinaryReaderSegment reader, Box header)
            : base(header)
        {
            this.maxContentLightLevel = reader.ReadUInt16();
            this.maxPicAverageLightLevel = reader.ReadUInt16();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentLightLevelInformationBox"/> class.
        /// </summary>
        /// <param name="maxContentLightLevel">The MaxCLL value, in nits.</param>
        /// <param name="maxPicAverageLightLevel">The MaxFALL value, in nits.</param>
        public ContentLightLevelInformationBox(ushort maxContentLightLevel, ushort maxPicAverageLightLevel)
            : base(BoxTypes.ContentLightLevelInformation)
        {
            this.maxContentLightLevel = maxContentLightLevel;
            this.maxPicAverageLightLevel = maxPicAverageLightLevel;
        }

        public ushort MaxContentLightLevel
        {
            get => this.maxContentLightLevel;
        }

        public ushort MaxPicAverageLightLevel
        {
            get => this.maxPicAverageLightLevel;
        }
    }
}
