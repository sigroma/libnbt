﻿using System;
using System.IO;
using System.IO.Compression;
using LibNbt.Exceptions;
using LibNbt.Queries;
using LibNbt.Tags;
using LZ4Sharp;

namespace LibNbt
{
    public class NbtFile : IDisposable
    {
        private const int BufferSize = 4096;

        protected byte[] FileContents { get; set; }

        protected string LoadedFile { get; set; }
        protected bool CompressedFile { get; set; }

        public NbtCompound RootTag { get; set; }
		
		private ILZ4Decompressor decompressor;
		private ILZ4Compressor compressor;

        public NbtFile() : this("") { }
        public NbtFile(string fileName) : this(fileName, true) { }
        public NbtFile(string fileName, bool compressed)
        {
            LoadedFile = fileName;
            CompressedFile = compressed;
			
			if(compressed)
            {
                decompressor = LZ4DecompressorFactory.CreateNew();
                compressor = LZ4CompressorFactory.CreateNew();
            }
        }

        public void Dispose()
        {
        }

        public virtual void LoadFile() { LoadFile(LoadedFile, CompressedFile); }

        public virtual void LoadFile(string fileName) { LoadFile(fileName, true); }
        public virtual void LoadFile(string fileName, bool compressed)
        {
            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException(string.Format("Could not find NBT file: {0}", fileName), fileName);
            }

            LoadedFile = fileName;
            CompressedFile = compressed;

            using (FileStream readFileStream = File.OpenRead(fileName))
            {
                LoadFile(readFileStream, CompressedFile);
            }
        }
        public virtual void LoadFile(Stream fileStream, bool compressed)
        {
            if (compressed)
            {
                byte[] data = new byte[fileStream.Length];
                fileStream.Read(data, 0, (int)fileStream.Length);
                data = decompressor.Decompress(data);
                MemoryStream memStream = new MemoryStream(data);
                LoadFileInternal(memStream);
            }
            else
            {
                LoadFileInternal(fileStream);
            }
        }
        protected void LoadFileInternal(Stream fileStream)
        {
            // Make sure the stream is at the beginning
            fileStream.Seek(0, SeekOrigin.Begin);

            // Make sure the first byte in this file is the tag for a TAG_Compound
            if (fileStream.ReadByte() == (int)NbtTagType.TAG_Compound)
            {
                var rootCompound = new NbtCompound();
                rootCompound.ReadTag(fileStream);

                RootTag = rootCompound;
            }
            else
            {
                throw new InvalidDataException("File format does not start with a TAG_Compound");
            }
        }

        public virtual void SaveFile(string fileName) { SaveFile(fileName, true); }
        public virtual void SaveFile(string fileName, bool compressed)
        {
            using (var saveStream = new MemoryStream())
            {
                SaveFile(saveStream, compressed);

                saveStream.Seek(0, SeekOrigin.Begin);
				
                string saveFileName = fileName + ".tmp";
                if (File.Exists(saveFileName)) { File.Delete(saveFileName); }
                using (FileStream saveFile = File.OpenWrite(saveFileName))
                {
                    var buffer = new byte[4096];
                    int bytesRead;
                    while ((bytesRead = saveStream.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        saveFile.Write(buffer, 0, bytesRead);
                    }
                }
                if (File.Exists(fileName)) { File.Delete(fileName); }
                File.Move (saveFileName, fileName);
            }
        }
        public virtual void SaveFile(Stream fileStream) { SaveFile(fileStream, true); }
        public virtual void SaveFile(Stream fileStream, bool compressed)
        {
            if (RootTag != null)
            {
                using (var memStream = new MemoryStream())
                {
                    var buffer = new byte[BufferSize];

                    RootTag.WriteTag(memStream);
                    memStream.Seek(0, SeekOrigin.Begin);

                    if (compressed)
                    {
                        byte[] data = memStream.ToArray();;
                        FileContents = compressor.Compress(data);
                    }
                    else
                    {
                        FileContents = new byte[memStream.Length];

                        int amtSaved, pos = 0;
                        while (pos < memStream.Length &&
                               (amtSaved = memStream.Read(buffer, pos, buffer.Length)) != 0)
                        {
                            Buffer.BlockCopy(buffer, 0, FileContents, pos, amtSaved);
                            pos += amtSaved;
                        }
                    }
                }

                int outPos = 0;
                while (outPos < FileContents.Length)
                {
                    int outAmt = 0;
                    if (BufferSize > FileContents.Length) { outAmt = FileContents.Length; }
                    fileStream.Write(FileContents, outPos, outAmt);
                    outPos += BufferSize;
                }
            }
        }

        public NbtTag Query(string query)
        {
            return Query<NbtTag>(query);
        }
        public T Query<T>(string query) where T : NbtTag
        {
            var tagQuery = new TagQuery(query);

            return RootTag.Query<T>(tagQuery);
        }
    }
}
