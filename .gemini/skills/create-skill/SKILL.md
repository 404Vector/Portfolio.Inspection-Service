---
name: create-skill
description: Create a new Gemini CLI skill (SKILL.md). Use when the user wants to add a custom slash command, automated behavior, or reusable instruction set to Gemini CLI.
---

# Create Gemini Skill

새로운 Gemini CLI skill을 생성합니다.
필수 frontmatter 필드를 포함한 `SKILL.md`를 작성합니다.

## 힌트 (선택)

$ARGUMENTS

---

## Frontmatter 필드 참조

새 skill에는 아래 **필수 필드**를 포함합니다.

| 필드 | 결정 기준 |
|------|-----------|
| `name` | Skill의 고유 식별자. 디렉토리 이름과 일치해야 합니다. |
| `description` | Skill이 하는 일과 Gemini가 언제 사용해야 하는지에 대한 설명입니다. |

---

## 디렉토리 구조 참조

skill은 `SKILL.md` 하나로도 동작하지만, 지원 파일을 추가하면 더 강력해집니다.

```
<skill-name>/
├── SKILL.md       (필수) 지침 및 메타데이터
├── scripts/       (선택) 실행 가능한 스크립트
├── references/    (선택) 정적 문서
└── assets/        (선택) 템플릿 및 기타 리소스
```

| 파일 유형 | 용도 |
|-----------|------|
| `scripts/` | 동적 데이터 수집·검증이 필요할 때 사용하는 **Python** 스크립트. |
| `references/` | Skill의 지침에 넣기에는 너무 큰 정적 참조 문서. |
| `assets/` | PR 설명, 커밋 메시지 등 고정 구조가 있는 출력 템플릿. |

### 스크립트 사용

`${GEMINI_SKILL_DIR}` 변수로 skill 디렉토리 내 스크립트를 절대 경로로 참조할 수 있습니다.

```
- 검증 결과: !`python3 ${GEMINI_SKILL_DIR}/scripts/validate.py 경로`
```
(backtick 앞의 `!`가 동적 주입 트리거)

지원 파일은 `SKILL.md` 안에서 명시적으로 참조합니다:

```markdown
## 추가 리소스
- 자세한 내용은 [references/api.md](references/api.md)를 참조하세요.
- 출력 형식은 [assets/template.md](assets/template.md)를 참조하세요.
```

---

## 실행 절차

### Step 1 — 정보 수집

`$ARGUMENTS`가 비어 있으면 아래 질문을 **한 번에** 출력하고 답변을 기다립니다:

> **1. Skill 이름:** (예: `review-pr`, `deploy`, `explain-code`)
> **2. 목적 설명:** Gemini가 자동으로 로드할 시기를 결정합니다. 트리거될 키워드를 포함하세요.
> **3. 지원 파일이 필요한가요?** (스크립트, 참조 문서, 템플릿 등)

`$ARGUMENTS`가 있으면 이를 이름·목적의 힌트로 활용합니다.

### Step 2 — 배치 위치 결정

다음 중 어디에 생성할지 사용자에게 선택하게 합니다:

- **프로젝트** (`.gemini/skills/<name>/SKILL.md`) — 이 저장소에서만 사용
- **개인** (`~/.gemini/skills/<name>/SKILL.md`) — 모든 프로젝트에서 사용

### Step 3 — SKILL.md 초안 제시

설계 결과를 아래 형식으로 **전체 초안**을 출력합니다.

```
---
name: <name>
description: <description>
---

<skill body>
```

초안 출력 후 확인을 요청합니다:

> 위 내용으로 skill을 생성하겠습니다. 수정할 사항이 있으면 알려주세요. (확인 / 수정)

**답변을 기다립니다.**

### Step 4 — 디렉토리 구조 설계

사용자 답변을 바탕으로 필요한 지원 파일과 디렉토리 구조를 결정합니다.

결정된 구조를 출력합니다:

```
<skill-name>/
├── SKILL.md
[├── scripts/]
[│   └── <script-name>.py]
[├── references/]
[│   └── <reference-doc>.md]
[└── assets/]
    [└── <asset-file>]
```

### Step 5 — 파일 생성

사용자가 확인하면 아래 순서로 생성합니다:

1. 루트 디렉토리: `mkdir -p <skill-dir>`
2. 하위 디렉토리(필요 시): `mkdir -p <skill-dir>/scripts`, `mkdir -p <skill-dir>/references`, `mkdir -p <skill-dir>/assets`
3. `SKILL.md` 작성
4. 지원 파일 작성 (필요 시)

### Step 6 — 생성된 SKILL.md 검증

파일 생성 후 `validate.py`로 구조를 검증합니다:

```bash
python3 ${GEMINI_SKILL_DIR}/scripts/validate.py <skill-dir>/SKILL.md
```

- ✓ 통과 → 완료 메시지 출력
- ✗ 실패 → 오류 목록 출력

완료 후 출력합니다:

> **생성 완료**
> ```
> <skill-dir>/
> ├── SKILL.md
> [└── ...]
> ```
> 테스트: `/<skill-name>` 을 입력하거나 관련 작업을 요청해보세요.
