        public static byte[] GetResetWorkTreeLinesAsPatch([NotNull] GitModule module, [NotNull] string text, int selectionPosition, int selectionLength, [NotNull] Encoding fileContentEncoding)
            string body = ToResetWorkTreeLinesPatch(selectedChunks);
        public static byte[] GetSelectedLinesAsPatch([NotNull] string text, int selectionPosition, int selectionLength, bool isIndex, [NotNull] Encoding fileContentEncoding, bool isNewFile)
            string body = ToIndexPatch(selectedChunks, isIndex, isWholeFile: false);
            var sb = new StringBuilder();
        public static byte[] GetSelectedLinesAsNewPatch([NotNull] GitModule module, [NotNull] string newFileName, [NotNull] string text, int selectionPosition, int selectionLength, [NotNull] Encoding fileContentEncoding, bool reset, byte[] filePreamble)
            var sb = new StringBuilder();
            var selectedChunks = FromNewFile(module, text, selectionPosition, selectionLength, reset, filePreamble, fileContentEncoding);
            string body = ToIndexPatch(selectedChunks, isIndex: false, isWholeFile: true);
                // if selection intersects with chunks
        private static IReadOnlyList<Chunk> FromNewFile([NotNull] GitModule module, [NotNull] string text, int selectionPosition, int selectionLength, bool reset, [NotNull] byte[] filePreamble, [NotNull] Encoding fileContentEncoding)
                Chunk.FromNewFile(module, text, selectionPosition, selectionLength, reset, filePreamble, fileContentEncoding)
        private static string ToResetWorkTreeLinesPatch([NotNull, ItemNotNull] IEnumerable<Chunk> chunks)
                return subChunk.ToResetWorkTreeLinesPatch(ref addedCount, ref removedCount, ref wereSelectedLines);
        private static string ToIndexPatch([NotNull, ItemNotNull] IEnumerable<Chunk> chunks, bool isIndex, bool isWholeFile)
                return subChunk.ToIndexPatch(ref addedCount, ref removedCount, ref wereSelectedLines, isIndex, isWholeFile);
        public string ToIndexPatch(ref int addedCount, ref int removedCount, ref bool wereSelectedLines, bool isIndex, bool isWholeFile)
                else if (!isIndex)
                else if (isIndex)
            if (PostContext.Count == 0 && (!isIndex || selectedLastRemovedLine))
            if (PostContext.Count == 0 && (selectedLastLine || isIndex || isWholeFile))
        public string ToResetWorkTreeLinesPatch(ref int addedCount, ref int removedCount, ref bool wereSelectedLines)
            var result = new Chunk();
                    // do not refactor, there are no break points condition in VS Express
        public static Chunk FromNewFile([NotNull] GitModule module, [NotNull] string fileText, int selectionPosition, int selectionLength, bool reset, [NotNull] byte[] filePreamble, [NotNull] Encoding fileContentEncoding)
            var result = new Chunk { _startLine = 0 };
                string preamble = i == 0 ? new string(fileContentEncoding.GetChars(filePreamble)) : string.Empty;