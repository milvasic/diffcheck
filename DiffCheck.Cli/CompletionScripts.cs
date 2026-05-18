namespace DiffCheck.Cli;

internal static class CompletionScripts
{
	internal const string Bash = """
		# diffcheck bash completion
		# Install: diffcheck completions bash > /etc/bash_completion.d/diffcheck
		#   or: diffcheck completions bash >> ~/.bashrc && source ~/.bashrc
		_diffcheck_completions() {
		    local cur prev words cword
		    _init_completion 2>/dev/null || {
		        COMPREPLY=()
		        cur="${COMP_WORDS[COMP_CWORD]}"
		        prev="${COMP_WORDS[COMP_CWORD-1]}"
		    }

		    local subcommands="list-profiles completions"
		    local opts="--output -o --format --column-map --key-columns --profile --save-profile \
		--case-insensitive --trim-whitespace --numeric-tolerance --match-threshold \
		--open --summary --fail-on-diff --max-added --max-removed --max-modified \
		--help --version"

		    # Second word: offer subcommands, options, and files
		    if [[ "${COMP_CWORD}" -eq 1 ]]; then
		        COMPREPLY=($(compgen -W "$subcommands $opts" -- "$cur"))
		        COMPREPLY+=($(compgen -f -- "$cur"))
		        return
		    fi

		    # Context-aware completions for specific options and subcommands
		    case "$prev" in
		        completions)
		            COMPREPLY=($(compgen -W "bash zsh fish" -- "$cur"))
		            return
		            ;;
		        --output|-o)
		            COMPREPLY=($(compgen -f -- "$cur"))
		            return
		            ;;
		        --format)
		            COMPREPLY=($(compgen -W "html json" -- "$cur"))
		            return
		            ;;
		    esac

		    # Default: complete files and options
		    COMPREPLY=($(compgen -W "$opts" -- "$cur"))
		    COMPREPLY+=($(compgen -f -- "$cur"))
		}

		complete -F _diffcheck_completions diffcheck
		""";

	internal const string Zsh = """
		#compdef diffcheck
		# diffcheck zsh completion
		# Install: diffcheck completions zsh > "${fpath[1]}/_diffcheck"
		#   then restart your shell or run: autoload -U compinit && compinit

		_diffcheck() {
		    local -a subcommands opts

		    subcommands=(
		        'list-profiles:List all saved comparison profiles'
		        'completions:Print shell completion script'
		    )

		    opts=(
		        '(--output -o)'{--output,-o}'[Path for the output report]:output file:_files'
		        '--format[Output format: html or json]:format:(html json)'
		        '--column-map[Column mapping (LeftHeader\:RightHeader)]:mapping'
		        '--key-columns[Key columns for row pairing]:columns'
		        '--profile[Load a saved profile by name]:profile'
		        '--save-profile[Save effective settings as a named profile]:profile name'
		        '--case-insensitive[Compare values case-insensitively]'
		        '--trim-whitespace[Strip leading/trailing whitespace before comparing]'
		        '--numeric-tolerance[Absolute tolerance for numeric comparisons]:tolerance'
		        '--match-threshold[Row-match threshold 0–1 (default 0.8)]:threshold'
		        '--open[Open the generated HTML report in the default browser]'
		        '--summary[Print diff counts to stdout; skip writing a report]'
		        '--fail-on-diff[Exit 1 if any differences are found]'
		        '--max-added[Exit 1 if added rows exceed this count]:count'
		        '--max-removed[Exit 1 if removed rows exceed this count]:count'
		        '--max-modified[Exit 1 if modified rows exceed this count]:count'
		        '(-h --help)'{-h,--help}'[Show help and exit]'
		        '--version[Show version and exit]'
		    )

		    if (( CURRENT == 2 )); then
		        _describe 'subcommand' subcommands
		        _arguments $opts
		        _files
		        return
		    fi

		    case "$words[2]" in
		        completions)
		            _arguments ':shell:(bash zsh fish)'
		            ;;
		        list-profiles)
		            ;;
		        *)
		            _arguments $opts
		            _files
		            ;;
		    esac
		}

		_diffcheck
		""";

	internal const string Fish = """
		# diffcheck fish completions
		# Install: diffcheck completions fish > ~/.config/fish/completions/diffcheck.fish

		# Disable default file completions at the top level so subcommands surface cleanly
		complete -c diffcheck -f

		# Subcommands
		complete -c diffcheck -n '__fish_use_subcommand' -a list-profiles -d 'List all saved comparison profiles'
		complete -c diffcheck -n '__fish_use_subcommand' -a completions    -d 'Print shell completion script'

		# completions subcommand — shell argument
		complete -c diffcheck -n '__fish_seen_subcommand_from completions' -a bash -d 'Bash completion script'
		complete -c diffcheck -n '__fish_seen_subcommand_from completions' -a zsh  -d 'Zsh completion script'
		complete -c diffcheck -n '__fish_seen_subcommand_from completions' -a fish -d 'Fish completion script'

		# Re-enable file completions for the root command (left/right file args)
		complete -c diffcheck -n 'not __fish_use_subcommand' -F

		# Options
		complete -c diffcheck -l output      -s o -d 'Path for the output report'                              -r
		complete -c diffcheck -l format           -d 'Output format: html (default) or json'                   -r -f -a 'html json'
		complete -c diffcheck -l column-map       -d 'Column mapping in LeftHeader:RightHeader format'         -r
		complete -c diffcheck -l key-columns      -d 'Key columns for row pairing (comma-separated or repeated)' -r
		complete -c diffcheck -l profile          -d 'Load a saved profile by name'                            -r
		complete -c diffcheck -l save-profile     -d 'Save effective settings as a named profile'              -r
		complete -c diffcheck -l case-insensitive -d 'Compare values case-insensitively'
		complete -c diffcheck -l trim-whitespace  -d 'Strip leading/trailing whitespace before comparing'
		complete -c diffcheck -l numeric-tolerance -d 'Absolute tolerance for numeric comparisons'             -r
		complete -c diffcheck -l match-threshold  -d 'Row-match threshold 0–1 (default 0.8)'                  -r
		complete -c diffcheck -l open             -d 'Open the generated HTML report in the default browser'
		complete -c diffcheck -l summary          -d 'Print diff counts to stdout; skip writing a report'
		complete -c diffcheck -l fail-on-diff     -d 'Exit 1 if any differences are found'
		complete -c diffcheck -l max-added        -d 'Exit 1 if added rows exceed this count'                  -r
		complete -c diffcheck -l max-removed      -d 'Exit 1 if removed rows exceed this count'               -r
		complete -c diffcheck -l max-modified     -d 'Exit 1 if modified rows exceed this count'              -r
		""";
}
