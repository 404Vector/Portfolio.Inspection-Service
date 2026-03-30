import subprocess
import json
import re

def run_command(command):
    """Runs a shell command and returns its output."""
    try:
        result = subprocess.run(
            command,
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            shell=True
        )
        return result.stdout.strip()
    except subprocess.CalledProcessError as e:
        return f"Error: {e.stderr.strip()}"

def get_git_info():
    """Collects and returns git repository information in a JSON format."""
    info = {}

    # Get staged, unstaged, and untracked files
    status_output = run_command('git status --porcelain')
    staged_files = []
    unstaged_files = []
    untracked_files = []
    if not status_output.startswith('Error:'):
        for line in status_output.splitlines():
            status = line[:2]
            filename = line[3:]
            if status[0] in 'MARC':
                staged_files.append(filename)
            if status[1] in 'MD':
                unstaged_files.append(filename)
            if status == '??':
                untracked_files.append(filename)

    info['staged_files'] = list(set(staged_files))
    info['unstaged_files'] = list(set(unstaged_files))
    info['untracked_files'] = untracked_files

    # Get diffs
    info['staged_diff'] = run_command('git diff --staged')
    info['unstaged_diff'] = run_command('git diff')

    # Get recent commit messages
    log_output = run_command("git log -n 5 --pretty=format:'%s'")
    if not log_output.startswith('Error:'):
        info['recent_commits'] = log_output.splitlines()
    else:
        info['recent_commits'] = []

    return info

if __name__ == "__main__":
    git_info = get_git_info()
    print(json.dumps(git_info, indent=2))
