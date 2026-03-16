<p align="center">
  <h1 align="center">UniLua</h1>
  <p align="center">
    네이티브 P/Invoke 기반 Unity용 Lua 5.5 스크립팅
  </p>
  <p align="center">
    <a href="https://opensource.org/licenses/MIT"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="License: MIT"></a>
    <img src="https://img.shields.io/badge/Unity-6-blue?logo=unity" alt="Unity 6">
    <img src="https://img.shields.io/badge/Lua-5.5-blue?logo=lua" alt="Lua 5.5">
  </p>
  <p align="center">
    <a href="README.md">English</a> | <b>한국어</b>
  </p>
</p>

---

UniLua는 네이티브 P/Invoke 바인딩을 통해 Unity에서 Lua 5.5 스크립팅을 지원합니다. Lua 스크립트 실행, C# 함수 등록, 리플렉션 브릿지를 통한 모든 C# 타입 접근이 가능합니다.

> **참고**: 현재 릴리스에는 **Windows x64** 용 네이티브 라이브러리만 포함되어 있습니다. 다른 플랫폼은 Lua 5.5 소스를 직접 빌드해야 합니다.

## 기능

- **Lua 5.5.0** 네이티브 라이브러리 (공식 소스 빌드)
- **P/Invoke 바인딩** — Lua C API 전체 지원
- **고수준 C# API** — `LuaEnv`, `LuaTable`, `LuaFunction`
- **CS. 리플렉션 브릿지** — 등록 없이 모든 C# 타입에 Lua에서 접근
  - 네임스페이스 체이닝: `CS.UnityEngine.GameObject`
  - 생성자, 정적/인스턴스 메서드, 프로퍼티, 필드
  - Enum 지원
  - 리플렉션 캐싱으로 성능 확보
- **Unity 통합** — `print()`가 `Debug.Log`로 리다이렉트
- **`.lua` ScriptedImporter** — 에디터에서 .lua 파일을 TextAsset으로 관리
- **IL2CPP 호환**

## 설치

Unity에서: **Window > Package Manager > + > Add package from git URL...**

```
https://github.com/inonego-unity/UniLua.git?path=com.inonego.uni-lua
```

또는 프로젝트의 `Packages/manifest.json`에 직접 추가:

```json
{
  "dependencies": {
    "com.inonego.uni-lua": "https://github.com/inonego-unity/UniLua.git?path=com.inonego.uni-lua"
  }
}
```

## 빠른 시작

### 기본 Lua 실행

```csharp
using inonego.UniLua;

var lua = new LuaEnv();

lua.DoString("print('Hello from Lua!')");

lua.SetGlobal("playerName", "Player1");
lua.DoString("print('Name: ' .. playerName)");

lua.Dispose();
```

### C# 함수 등록

```csharp
var lua = new LuaEnv();

lua.RegisterFunction("Heal", (env) =>
{
    double amount = env.ToNumber(1);
    Debug.Log($"체력 {amount} 회복!");
    return 0;
});

lua.DoString("Heal(50)");
```

### C#에서 Lua 함수 호출

```csharp
var lua = new LuaEnv();

lua.DoString("function Add(a, b) return a + b end");
object[] result = lua.Call("Add", 10, 20);
// result[0] = 30
```

### CS. 리플렉션 브릿지

등록 없이 모든 C# 타입에 Lua에서 직접 접근:

```lua
-- 정적 메서드
CS.UnityEngine.Debug.Log("Hello from Lua!")

-- 생성자
local go = CS.UnityEngine.GameObject("MyObject")

-- 인스턴스 프로퍼티 / 메서드
print(go.name)
go:SetActive(false)

-- 정적 메서드
local found = CS.UnityEngine.GameObject.Find("MyObject")

-- 정리
CS.UnityEngine.Object.Destroy(go)

-- Enum
local space = CS.UnityEngine.KeyCode.Space

-- 커스텀 클래스도 가능
local custom = CS.MyGame.EnemyController()
```

## API 레퍼런스

### LuaEnv

| 메서드 | 설명 |
|--------|------|
| `DoString(code)` | Lua 코드 문자열 실행 |
| `DoFile(path)` | Lua 파일 실행 |
| `Call(name, args...)` | 글로벌 Lua 함수 호출 |
| `RegisterFunction(name, callback)` | C# 함수를 Lua에 등록 |
| `SetGlobal(name, value)` | 글로벌 변수 설정 |
| `GetGlobal(name)` | 글로벌 변수 읽기 |
| `Push(value)` | Lua 스택에 값 푸시 |
| `ToObject(index)` | Lua 스택에서 값 읽기 |
| `Dispose()` | Lua 상태 닫기 및 리소스 해제 |

### LuaTable

| 메서드 | 설명 |
|--------|------|
| `Get(key)` | 문자열 또는 정수 키로 값 읽기 |
| `Set(key, value)` | 문자열 또는 정수 키로 값 설정 |
| `Length` | 테이블의 raw 길이 (# 연산자) |
| `ForEach(callback)` | 모든 키-값 쌍 순회 |

### LuaFunction

| 메서드 | 설명 |
|--------|------|
| `Call(args...)` | Lua 함수 호출 |

## 프로젝트 구조

```
Runtime/
├── Native/
│   ├── LuaConst.cs           # Lua 5.5 상수
│   └── LuaNative.cs          # P/Invoke 선언
├── Core/
│   ├── LuaEnv.cs             # 고수준 Lua 상태 래퍼
│   ├── LuaException.cs       # 에러 코드 포함 예외
│   ├── LuaTable.cs           # 테이블 참조 래퍼
│   └── LuaFunction.cs        # 함수 참조 래퍼
├── Bridge/
│   ├── ReflectionCache.cs    # 타입/멤버/오버로드/델리게이트 캐싱
│   ├── ObjectBridge.cs       # CS. 중앙 오케스트레이터
│   ├── NamespaceProxy.cs     # CS.Namespace.Type 체이닝
│   ├── TypeProxy.cs          # 정적 멤버 + 생성자
│   ├── ObjectWrapper.cs      # 인스턴스 userdata + 메타메서드
│   ├── MethodWrapper.cs      # 메서드 호출 + 오버로드 해소
│   └── EnumWrapper.cs        # Enum 값 접근
└── Plugins/Lua/
    └── Windows/x86_64/lua55.dll
Editor/
└── LuaScriptImporter.cs      # .lua ScriptedImporter
```

## 알려진 제한 사항

- **IL2CPP 스트리핑**: 리플렉션으로만 접근하는 타입은 `[Preserve]` 또는 `link.xml` 필요
- **값 타입 박싱**: 구조체(Vector3 등)가 userdata로 래핑될 때 boxing 발생
- **제네릭 메서드**: IL2CPP에서 값 타입에 대한 `MakeGenericMethod` 제한
- **Windows x64 전용** (현재): 다른 플랫폼은 lua55 소스 빌드 필요

## 라이선스

[MIT](LICENSE)
