---
name: create-skill
description: Create a new Claude Code skill (SKILL.md). Use when the user wants to add a custom slash command, automated behavior, or reusable instruction set to Claude Code.
argument-hint: [skill-name] [brief purpose]
disable-model-invocation: true
user-invocable: true
allowed-tools: Read, Write, Glob, Bash(mkdir *)
model: sonnet
context: ~
agent: ~
hooks: ~
---

# Create Skill

새로운 Claude Code skill을 생성합니다.
모든 frontmatter 필드를 생략 없이 명시적으로 선언한 `SKILL.md`를 작성합니다.

## 힌트 (선택)

$ARGUMENTS

---

## Frontmatter 필드 참조

새 skill에는 아래 **모든 필드**를 포함합니다. 적용되지 않는 필드는 `~`(null)로 명시합니다.

| 필드 | 기본값 | 결정 기준 |
|------|--------|-----------|
| `name` | 디렉토리 이름 | `/name` slash command. 소문자·숫자·하이픈, 최대 64자 |
| `description` | 첫 단락 | Claude가 자동 로드 여부를 판단. 트리거 키워드를 포함 |
| `argument-hint` | `~` | 자동완성 힌트. 예: `[issue-number]`, `[filename] [format]` |
| `disable-model-invocation` | `false` | 부작용 있는 워크플로우(배포·커밋·외부 호출)이면 `true` |
| `user-invocable` | `true` | 배경 지식 전용(직접 호출 불필요)이면 `false` |
| `allowed-tools` | `~` | 사용자 승인 없이 허용할 도구 목록 |
| `model` | 상속 | 이 skill 전용 모델. 불필요하면 `~` |
| `context` | 인라인 | 격리 실행이 필요하면 `fork` (대화 히스토리 미공유), 아니면 `~` |
| `agent` | `general-purpose` | `context: fork`일 때만 의미 있음. `Explore`, `Plan`, `general-purpose` 중 선택 |
| `hooks` | `~` | skill 라이프사이클 hooks. 불필요하면 `~` |

---

## 디렉토리 구조 참조

skill은 `SKILL.md` 하나로도 동작하지만, 지원 파일을 추가하면 더 강력해집니다.
`SKILL.md`는 500줄 이하로 유지하고 상세 내용은 별도 파일로 분리합니다.

```
<skill-name>/
├── SKILL.md              # 주요 지침 (필수)
├── template.md           # Claude가 채울 출력 템플릿
├── examples/
│   └── sample.md         # 예상 출력 형식을 보여주는 예제
└── scripts/
    └── validate.sh       # Claude가 실행할 수 있는 스크립트
```

| 파일 유형 | 용도 | 언제 추가하는가 |
|-----------|------|----------------|
| `template.md` | 고정 구조가 있는 출력(PR 설명, 커밋 메시지 등) | 형식을 강제해야 할 때 |
| `examples/` | Claude가 참고할 구체적인 출력 예시 | 형식이 복잡하거나 애매할 때 |
| `scripts/` | Shell·Python 등 실행 가능한 스크립트 | 동적 데이터 수집·검증이 필요할 때 |
| `reference.md` | 대형 API 문서, 규칙집 등 | 항상 로드하기엔 너무 큰 참조 문서 |

### 스크립트 우선 원칙 (컨텍스트 최소화)

컨텍스트에 로드되는 내용이 많을수록 성능이 떨어지고 비용이 증가합니다.
**작업을 스크립트로 수행할 수 있다면 정적 파일 대신 스크립트를 작성합니다.**

| 상황 | 비선호 방식 | 선호 방식 |
|------|------------|----------|
| 현재 git 상태 파악 | `context.md`에 설명 나열 | `!`git status --short`` 동적 주입 |
| 코드베이스 규칙 검증 | `reference.md`를 통째로 로드 | `scripts/lint.sh`를 실행해 결과만 주입 |
| 대형 API 문서 참조 | `reference.md` 전체 로드 | 스크립트로 필요한 항목만 추출 후 주입 |
| 파일 목록 수집 | Claude가 Glob 반복 실행 | `scripts/collect.sh`로 한 번에 수집 |

동적 주입 구문(`!`command``): skill 실행 전 명령어 출력을 컨텍스트에 삽입합니다.

```yaml
## 현재 상태
- 브랜치: !`git branch --show-current`
- 변경 파일: !`git diff --name-only HEAD`
```

`${CLAUDE_SKILL_DIR}` 변수로 skill 디렉토리 내 스크립트를 절대 경로로 참조합니다:

```yaml
- 검증 결과: !`bash ${CLAUDE_SKILL_DIR}/scripts/validate.sh`
```

정적 파일(`template.md`, `examples/`)은 스크립트로 대체할 수 없는 경우에만 사용합니다.
지원 파일은 `SKILL.md` 안에서 명시적으로 참조합니다:

```markdown
## 추가 리소스
- 출력 형식은 [template.md](template.md) 참조
- 예제는 [examples/sample.md](examples/sample.md) 참조
```

---

## 실행 절차

### Step 1 — 정보 수집

`$ARGUMENTS`가 비어 있으면 아래 질문을 **한 번에** 출력하고 답변을 기다립니다:

> **1. Skill 이름:** (예: `review-pr`, `deploy`, `explain-code`)
> **2. 목적 설명:** Claude가 자동으로 로드할 시기를 결정합니다. 트리거될 키워드를 포함하세요.
> **3. 사용자가 직접 인수를 전달하나요?** 예라면 인수 형태도 설명해주세요.
> **4. 지원 파일이 필요한가요?** 출력 템플릿, 예제, 실행 스크립트, 대형 참조 문서 등이 있으면 알려주세요.

`$ARGUMENTS`가 있으면 이를 이름·목적의 힌트로 활용합니다.

### Step 2 — 배치 위치 결정

다음 중 어디에 생성할지 사용자에게 선택하게 합니다:

- **프로젝트** (`.claude/skills/<name>/SKILL.md`) — 이 저장소에서만 사용
- **개인** (`~/.claude/skills/<name>/SKILL.md`) — 모든 프로젝트에서 사용

### Step 3 — Frontmatter 설계

사용자 설명을 바탕으로 각 frontmatter 값을 결정하고 그 이유를 간략히 설명합니다.

결정 기준:
- 배포·커밋·Slack 전송 등 부작용 있음 → `disable-model-invocation: true`
- `/` 메뉴 노출 불필요, Claude 배경 지식 전용 → `user-invocable: false`
- 대화 히스토리 없이 독립 실행 → `context: fork` + 적합한 `agent`
- 특정 도구만 허용 → `allowed-tools`에 명시

### Step 4 — SKILL.md 초안 제시

설계 결과를 아래 형식으로 **전체 초안**을 출력합니다.
값이 없는 필드는 반드시 `~`로 작성합니다:

```
---
name: <name>
description: <description>
argument-hint: <hint or ~>
disable-model-invocation: <true|false>
user-invocable: <true|false>
allowed-tools: <tools or ~>
model: <model or ~>
context: <fork or ~>
agent: <agent-type or ~>
hooks: ~
---

<skill body>
```

초안 출력 후 확인을 요청합니다:

> 위 내용으로 skill을 생성하겠습니다. 수정할 사항이 있으면 알려주세요. (확인 / 수정)

**답변을 기다립니다.**

### Step 5 — 디렉토리 구조 설계

지원 파일이 필요한지 판단하고, 필요하다면 어떤 파일을 만들지 결정합니다.

**스크립트 우선으로 판단합니다.** 컨텍스트 크기는 성능·비용에 직결됩니다.

판단 순서:

1. **먼저 스크립트화 가능성을 검토합니다**
   - 작업이 데이터 수집·검증·변환을 포함하는가?
   - 결과를 `!`command`` 동적 주입으로 넘길 수 있는가?
   - 가능하다면 → `scripts/` 작성 후 `!`bash ${CLAUDE_SKILL_DIR}/scripts/...`` 로 주입

2. **스크립트로 대체 불가한 경우에만 정적 파일을 추가합니다**
   - 고정 출력 형식이 있는가? → `template.md`
   - 형식이 복잡해 예시가 필요한가? → `examples/sample.md`
   - 참조 문서가 너무 커서 SKILL.md에 넣기 어려운가? → `reference.md` (필요 시만 로드)

3. **지원 파일이 전혀 없어도 되는가?** → `SKILL.md`만 생성

결정된 구조를 출력합니다:

```
<skill-name>/
├── SKILL.md
[├── template.md]
[├── examples/]
[│   └── sample.md]
[└── scripts/]
[    └── <script-name>]
```

### Step 6 — 파일 생성

사용자가 확인하면 아래 순서로 생성합니다:

1. 루트 디렉토리: `mkdir -p <skill-dir>`
2. 하위 디렉토리(필요 시): `mkdir -p <skill-dir>/examples`, `mkdir -p <skill-dir>/scripts`
3. `SKILL.md` 작성 — 지원 파일이 있으면 `## 추가 리소스` 섹션에서 명시적으로 참조
4. 지원 파일 작성 (template.md, examples/sample.md, scripts/ 등)

지원 파일에서 스크립트를 참조할 때는 `${CLAUDE_SKILL_DIR}` 변수를 사용합니다.

완료 후 출력합니다:

> **생성 완료**
> ```
> <skill-dir>/
> ├── SKILL.md
> [└── ...]
> ```
> 테스트: `/<skill-name>` 을 입력하거나 관련 작업을 요청해보세요.
