using System;
using UnityEngine;

namespace MemoryProtocol
{
    public sealed partial class AssemblyMazePuzzle
    {
        // Nine tile spaces, five authored fixed tiles and four freely interchangeable fragments.
        public RectTransform socketBoard;
        public RectTransform[] sockets = Array.Empty<RectTransform>();
        int[] attachedSockets;
        public bool UsesSocketBoard => socketBoard != null;
        public bool AllSocketsFilled => attachedSockets != null && attachedSockets.Length == 4 && Array.TrueForAll(attachedSockets, s => s >= 0);
        public int AttachedSocket(int piece) => attachedSockets[piece];

        void InitializeSocketBoard()
        {
            CapturePositions();
            groups = new int[pieces.Length];
            attachedSockets = new int[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                groups[i] = i; attachedSockets[i] = -1;
                pieces[i].anchoredPosition = initialPositions[i];
                pieces[i].gameObject.SetActive(true);
            }
            SelectedPiece = -1; pulse = 0;
            foreach (var image in joinFeedback) image.enabled = false;
            motor.Initialize(this, movementSettings, entrance);
            SyncPresentation(); PlaceMarker(); RefreshSelectionHighlight();
        }

        public void ResetSocketProgress()
        {
            motor.Rebase(entrance); PlaceMarker();
        }

        void DetachSocket(int index)
        {
            if (attachedSockets[index] < 0) return;
            attachedSockets[index] = -1; ResetSocketProgress();
        }

        void DragSocketPiece(int index, Vector2 delta)
        {
            DetachSocket(index);
            var piece = pieces[index];
            var pos = piece.anchoredPosition + delta;
            pos.x = Mathf.Clamp(pos.x, 0, movementArea.rect.width - piece.rect.width);
            pos.y = Mathf.Clamp(pos.y, -movementArea.rect.height + piece.rect.height, 0);
            piece.anchoredPosition = pos;
        }

        Vector2 SocketPosition(int piece, int slot) =>
            pieces[piece].parent.InverseTransformPoint(sockets[slot].position);

        bool SnapToSocket(int index)
        {
            int best = -1; float distance = snapDistance;
            for (int slot = 0; slot < sockets.Length; slot++)
            {
                if (Array.IndexOf(attachedSockets, slot) >= 0) continue;
                float candidate = Vector2.Distance(pieces[index].anchoredPosition, SocketPosition(index, slot));
                if (candidate > distance) continue;
                best = slot; distance = candidate;
            }
            if (best < 0) return false;
            attachedSockets[index] = best;
            SyncSocketPieces(); ResetSocketProgress(); pulse = snapPulseDuration;
            RefreshSelectionHighlight(); return true;
        }

        public void SyncSocketPieces()
        {
            if (attachedSockets == null) return;
            for (int i = 0; i < pieces.Length; i++)
                if (attachedSockets[i] >= 0) pieces[i].anchoredPosition = SocketPosition(i, attachedSockets[i]);
        }

        bool SocketCanOccupy(Vector2Int cell)
        {
            if (cell.x < 0 || cell.y < 0 || cell.x >= 3 * pieceDimensions.x || cell.y >= 3 * pieceDimensions.y) return false;
            var tile = new Vector2Int(cell.x / pieceDimensions.x, cell.y / pieceDimensions.y);
            int hole = Array.IndexOf(slots, tile);
            if (hole >= 0)
            {
                if (attachedSockets == null) return false;
                int piece = Array.IndexOf(attachedSockets, hole);
                if (piece < 0) return false;
                cell += (slots[piece] - tile) * pieceDimensions;
            }
            return Array.IndexOf(floorCells, cell) >= 0;
        }

        Vector3 SocketCellWorldPosition(Vector2Int cell)
        {
            Vector2 size = socketBoard.rect.size / ((Vector2)pieceDimensions * 3);
            return socketBoard.TransformPoint(new Vector3(socketBoard.rect.xMin + (cell.x + .5f) * size.x,
                socketBoard.rect.yMax - (cell.y + .5f) * size.y));
        }

        void PlaceSocketMarker()
        {
            var cell = motor.Current;
            int slot = Array.IndexOf(slots, new Vector2Int(cell.x / pieceDimensions.x, cell.y / pieceDimensions.y));
            int piece = slot >= 0 && attachedSockets != null ? Array.IndexOf(attachedSockets, slot) : -1;
            Transform parent = piece >= 0 ? pieces[piece] : transform;
            if (playerMarker.transform.parent != parent) playerMarker.transform.SetParent(parent, true);
            playerMarker.rectTransform.position = motor.WorldPosition;
        }
    }
}
