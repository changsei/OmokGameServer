using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server_Omok_Game
{
    public class OmokServerGameHandler
    {
        private static readonly int MAX_BOARD_LENGTH = 19;
        public enum STONE { none, black, white };

        public STONE[,] Board { get; private set; } = new STONE[MAX_BOARD_LENGTH, MAX_BOARD_LENGTH];
        public int CurrentTurn { get; private set; } = 1;  // 1: Main, 2: Sub

        public OmokServerGameHandler()
        {
            Initialize();
        }

        public void Initialize()
        {
            CurrentTurn = 1;

            for (int i = 0; i < MAX_BOARD_LENGTH; i++)
            {
                for (int j = 0; j < MAX_BOARD_LENGTH; j++)
                {
                    Board[i, j] = STONE.none;
                }
            }
        }

        public bool PlaceStone(int x, int y)
        {
            Board[x, y] = CurrentTurn == 1 ? STONE.black : STONE.white;
            if (CheckForWin(x, y)) return true;
            ChangeTurn();
            return false;
        }

        private void ChangeTurn()
        {
            CurrentTurn = CurrentTurn == 1 ? 2 : 1;
        }

        /*        public bool CheckForWin(int x, int y)
                {
                    return false;
                }*/

        public bool CheckForWin(int x, int y)
        {
            STONE currentStone = Board[x, y];
            if (currentStone == STONE.none) return false; // 현재 위치에 돌이 없다면 승리 불가능

            // 검사할 방향 정의: 수평, 수직, 주 대각선, 부 대각선
            int[][] directions = new int[][]
            {
                new int[] {1, 0}, // 수평 방향 (오른쪽)
                new int[] {0, 1}, // 수직 방향 (아래)
                new int[] {1, 1}, // 주 대각선 방향 (오른쪽 아래)
                new int[] {1, -1} // 부 대각선 방향 (오른쪽 위)
            };

            // 모든 방향에 대해 검사
            foreach (var dir in directions)
            {
                int count = 1; // 현재 돌도 카운트에 포함

                // 각 방향에 대해 양쪽으로 탐색
                for (int i = 1; i < 5; i++)
                {
                    // 앞쪽 방향으로 탐색
                    if (InBounds(x + i * dir[0], y + i * dir[1]) && Board[x + i * dir[0], y + i * dir[1]] == currentStone)
                        count++;
                    else
                        break; // 연속된 돌이 끊기면 중단
                }

                for (int i = 1; i < 5; i++)
                {
                    // 뒤쪽 방향으로 탐색
                    if (InBounds(x - i * dir[0], y - i * dir[1]) && Board[x - i * dir[0], y - i * dir[1]] == currentStone)
                        count++;
                    else
                        break; // 연속된 돌이 끊기면 중단
                }

                // 5개 이상의 돌이 연속되면 승리
                if (count >= 5) return true;
            }

            return false; // 모든 방향을 검사해도 5개 이상이 되지 않으면 승리하지 않음
        }

        // 보드 범위 내에 있는지 확인하는 함수
        private bool InBounds(int x, int y)
        {
            return x >= 0 && x < MAX_BOARD_LENGTH && y >= 0 && y < MAX_BOARD_LENGTH;
        }
    }
}
