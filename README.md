C# 과목 과제용 프로젝트
.Net Frame 워크로 만든 오목 게임 서버 입니다.


#개발 일지

2024/05/15
- 로그인
- ID 조회
- PW 변경
- 계정 탈퇴

2024/05/17
- 전체 채팅
- 게임룸 입장(임시)

특이사항
- 게임룸 입장시 다른 클라이언트가 전체 채팅을 전송하면 충돌발생
- 게임룸 강제 종료시 네트워크 끊김
- 메인 서버의 데이터 분류 로직을 전체적으로 리팩토링 할 필요가 있음 

2024/05/20
- 데이터 분류 리팩토링
- 게임룸 강제 종료시 발생하는 버그 수정
  
특이사항
- 채팅 기능을 현재 클라이언트가 속한 방 정보를 바탕으로 송수신 가능하도록 구현 예정
- 유저 목록 동기화 기능 추가, 게임 방 전용 채팅 기능 도입 예정
- 게임 준비 기능 추가 예정
- 오목 게임 추가 예정

2024/05/21
- 게임방 내부 채팅 구현
- 게임방에 접속시, 다른 유저가 전체 채팅 기능을 사용했을때, 네트워크가 끊기는 현상 수정

특이사항
- 오목 게임 추가 예정

2024/05/22
- 오목 게임 일부 구현/재시작/기권/퇴장
- 중복 로그인

특이사항
- 오목 예외 처리 구현
- 오목 승리 구현

2024/05/24
- 오목 승리 검사 구현
- 클라이언트 코드 https://github.com/changsei/OmokGameUI
