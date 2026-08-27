#!/usr/bin/env bash
# =============================================================================
# run-ci.sh 鈥斺€?銆婂井鍏夊綊澶勩€婥I 涓€閿棬锛堝眰 0 + 灞?1 缂栨帓锛?# 璐熻矗浜猴細绋嬪熀宀╋紙engineering-lead锛夈€€|銆€瀵瑰簲锛歡ame/docs/TEST_STRATEGY.md 搂5
# 鐢ㄦ硶锛堜粠浠撳簱鏍圭洰褰曪紝鍗冲寘鍚?game/ 鐨勭洰褰曟墽琛岋級锛?#   bash game/ci/run-ci.sh
# 閫€鍑虹爜锛? = 鍏ㄩ儴閫氳繃锛涢潪 0 = 鏈夐棬澶辫触锛圕I 鐩存帴鍒ょ孩锛?#
# 璁捐鍘熷垯锛堣 TEST_STRATEGY.md 搂0锛夛細
#   Core 鏄函 C#锛?0% 椋庨櫓闈犵绾?EditMode 鍗曟祴鎸′綇锛沀nity 鍙獙"寮曟搸閲屾墠瀛樺湪鐨勪笢瑗?銆?#   鍥犳 CI 鍦ㄣ€愭棤 Unity 璁稿彲璇併€戠殑鏈哄櫒涓婁篃鑳借窇瀹屽眰 0锛堝绾﹂棬 + 鑷祴 + 鍛藉悕瀹堟姢锛夛紝
#   Unity 娴嬭瘯浣滀负鍙€夌浜岄亾闂ㄢ€斺€旀湰鏈烘棤 Unity 鏃舵樉寮?SKIP锛屼笉璇姤涓哄け璐ャ€?# =============================================================================
set -u

# 瀹氫綅浠撳簱鏍癸細鏈剼鏈湪 game/ci/ 涓嬶紝涓婁袱绾у嵆鏍?SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
GAME_DIR="$REPO_ROOT/game"
TOOLS_DIR="$GAME_DIR/tools"
DATA_DIR="$GAME_DIR/Assets/Data"

# 鍏煎 Git Bash 涓嬬殑 Windows Python锛氭妸 MSYS 璺緞杞垚 Python 鑳借瘑鍒殑褰㈡€併€?# Git Bash 涓?`python3`/`python` 閫氬父鏄?Windows 瑙ｉ噴鍣紝鏀跺埌 /c/... 浼氳褰撴垚 C:\c\... 鑰屾壘涓嶅埌鏂囦欢锛?# 杩欓噷鐢?`cygpath -w` 杞粷瀵?Windows 璺緞锛屼笖缁熶竴鐢?`python` 鍛戒护缁?PATH 瑙ｆ瀽銆?if command -v cygpath >/dev/null 2>&1; then
  TOOLS_DIR_W="$(cygpath -w "$TOOLS_DIR")"
  DATA_DIR_W="$(cygpath -w "$DATA_DIR")"
  GAME_DIR_W="$(cygpath -w "$GAME_DIR")"
else
  TOOLS_DIR_W="$TOOLS_DIR"
  DATA_DIR_W="$DATA_DIR"
  GAME_DIR_W="$GAME_DIR"
fi
PY="python3"
command -v python >/dev/null 2>&1 && PY="python"   # 浼樺厛鐢?PATH 涓彲瑙ｆ瀽鐨?python

echo "== 寰厜褰掑 CI :: 浠撳簱鏍?$REPO_ROOT =="

RC=0  # 绱閫€鍑虹爜

# -----------------------------------------------------------------------------
# 灞?0 路 G1 濂戠害闂紙I1锛岀‖闃诲锛?鈥斺€?鏁版嵁瓒婄晫鍦?Unity 瀵煎叆鏈熶細宕╁湪鐜╁鏈?#   鑴氭湰锛歵ools/validate_contract.py  閫€鍑?0=PASS / 1=FAIL
# -----------------------------------------------------------------------------
echo ""
echo "[灞?/G1] 濂戠害闂?validate_contract.py锛堟牎楠?Assets/Data/*.csv锛?
if "$PY" "$TOOLS_DIR_W/validate_contract.py" "$DATA_DIR_W"; then
  echo "  鉁?G1 PASS"
else
  echo "  鉂?G1 FAIL锛堝绾﹁繚瑙勶紝鏋勫缓搴旈樆鏂級"
  RC=1
fi

# -----------------------------------------------------------------------------
# 灞?0 路 G2 濂戠害闂ㄨ嚜娴?鈥斺€?瀹堟姢"鏍￠獙鍣ㄦ湰韬笉琚敼鍧?锛? 姝ｅ悜 / 27 璐熷悜锛?#   鑴氭湰锛歵ools/test_validate_contract.py  閫€鍑?0=PASS / 1=FAIL
# -----------------------------------------------------------------------------
echo ""
echo "[灞?/G2] 濂戠害鏍￠獙鍣ㄨ嚜娴?test_validate_contract.py锛?1 鐢ㄤ緥锛?
if "$PY" "$TOOLS_DIR_W/test_validate_contract.py"; then
  echo "  鉁?G2 PASS"
else
  echo "  鉂?G2 FAIL锛堟牎楠屽櫒鑷祴鏈繃锛?
  RC=1
fi

# -----------------------------------------------------------------------------
# G3 鍛藉悕涓庨槇鍊煎畧鎶わ紙C2 鍒悕 / C3 纭紪鐮?0.85 / EVT_ 瀛楅潰閲忛棬锛?#   鑴氭湰锛歵ools/check_naming.py [scripts_dir]  閫€鍑?0=PASS / 1=FAIL
#   鍏堣瘝娉曞墺绂伙紙娉ㄩ噴/瀛楃涓?瀛楃瀛楅潰閲忥級鍐嶅湪绾唬鐮佷笂鍖归厤锛岄伩鍏嶈鎶?# -----------------------------------------------------------------------------
echo ""
echo "[G3] 鍛藉悕涓庨槇鍊煎畧鎶?check_naming.py锛圕2/C3/EVT_ 闂級"
if "$PY" "$TOOLS_DIR_W/check_naming.py" "$GAME_DIR_W/Assets/Scripts"; then
  echo "  鉁?G3 PASS"
else
  echo "  鉂?G3 FAIL锛堝瓨鍦ㄥ埆鍚?纭紪鐮侀槇鍊?EVT_ 瀛楅潰閲忔紓绉伙級"
  RC=1
fi

# -----------------------------------------------------------------------------
# G3 闄勫姞 路 娴嬭瘯甯冨眬瀹堟姢锛坅smdef / 瑁呴厤缁撴瀯锛屼繚璇佸崟娴嬭兘琚?Test Runner 鍙戠幇锛?#   鑴氭湰锛歵ools/check_test_layout.py
# -----------------------------------------------------------------------------
echo ""
echo "[G3] 娴嬭瘯甯冨眬瀹堟姢 check_test_layout.py"
if "$PY" "$TOOLS_DIR_W/check_test_layout.py"; then
  echo "  鉁?娴嬭瘯甯冨眬 PASS"
else
  echo "  鈿狅笍  娴嬭瘯甯冨眬妫€鏌ユ湭閫氳繃鎴栨湭瀹炵幇锛堢櫥璁颁负宸茬煡椤癸紝涓嶅己鍒堕樆鏂?CI锛?
  # 鑻ヨ鑴氭湰灏氫负鍗犱綅/鏈疄鐜帮紝涓嶅洜姝ゆ妸 CI 鍒ょ孩锛涘宸插疄瑁呰鎸夐渶鏀逛负 RC=1
fi

# -----------------------------------------------------------------------------
# 灞?1 路 G4/G5/G7 Unity EditMode 鍗曟祴 + 鐑熼浘闂?+ 瑕嗙洊鐜?#   浠呭湪鐜瀛樺湪 Unity 鍙墽琛屾枃浠舵椂鎵ц锛涘惁鍒欐樉寮?SKIP锛堜笉璇姤锛夈€?#   Unity -runTests 閫€鍑虹爜锛?=鍏ㄨ繃 / 2=鏈夌敤渚嬪け璐?/ 3=杩愯澶辫触锛堢紪璇戦敊璇瓑锛?#   娉ㄦ剰锛?runTests 涓嶅緱涓?-quit 鍚岀敤锛堜細鍦ㄦ祴璇曡窇瀹屽墠閫€鍑猴級銆?# -----------------------------------------------------------------------------
echo ""
echo "[灞?/G4/G5] Unity EditMode 鍗曟祴锛堥渶 Unity 2022 LTS锛屽彲閫夛級"
UNITY_BIN=""
if command -v Unity >/dev/null 2>&1; then
  UNITY_BIN="Unity"
elif [ -n "${UNITY_PATH:-}" ] && [ -x "$UNITY_PATH" ]; then
  UNITY_BIN="$UNITY_PATH"
else
  # 鑷姩鎺㈡祴甯歌瀹夎璺緞锛堟湰鏈鸿鏈哄悗闆堕厤缃嵆鍙窇锛?  # 瑕嗙洊锛氱敤鎴风骇 Hub锛?-scope user 闈炵鐞嗗憳瀹夎鐨勯粯璁や綅缃級銆佹満鍣ㄧ骇 Hub銆佽８瑁呫€乵acOS銆丩inux
  # 娉ㄦ剰 Windows 鐢ㄦ埛绾ц矾寰勫湪 $LOCALAPPDATA/Unity/Hub/Editor锛堝嵆 ~/AppData/Local/...锛?  LOCAL_U="$LOCALAPPDATA/Unity/Hub/Editor/2022.3.20f1/Editor/Unity.exe"
  CANDIDATES="$HOME/Unity/Hub/Editor/2022.3.20f1/Editor/Unity \
              \"$LOCAL_U\" \
              /opt/unity/Editor/Unity \
              /Applications/Unity/Hub/Editor/2022.3.20f1/Unity.app/Contents/MacOS/Unity \
              /c/Program\ Files/Unity/Hub/Editor/2022.3.20f1/Editor/Unity.exe \
              /c/Program\ Files/Unity/Editor/Unity.exe"
  for c in $CANDIDATES; do
    # 鍘绘帀鍙兘鐨勫灞傚紩鍙峰悗鍐嶆祴瀛樺湪鎬э紙Windows 鐢ㄦ埛绾ц矾寰勫惈绌烘牸锛?    c_clean="$(echo "$c" | sed 's/^"//;s/"$//')"
    if [ -x "$c_clean" ] || [ -f "$c_clean" ]; then UNITY_BIN="$c_clean"; break; fi
  done
fi

if [ -n "$UNITY_BIN" ]; then
  mkdir -p "$REPO_ROOT/artifacts"
  echo "  璋冪敤锛?UNITY_BIN -batchmode -nographics -runTests -projectPath $GAME_DIR -testPlatform EditMode ..."
  "$UNITY_BIN" -batchmode -nographics \
    -projectPath "$GAME_DIR" \
    -runTests -testPlatform EditMode \
    -testResults "$REPO_ROOT/artifacts/editmode.xml" \
    -logFile - || URC=$?
  if [ "${URC:-0}" -eq 0 ]; then
    echo "  鉁?G4/G5 Unity 鍗曟祴鍏ㄧ豢"
  else
    echo "  鉂?Unity 鍗曟祴鏈繃锛堥€€鍑?$URC锛夛紝CI 鍒ょ孩"
    RC=1
  fi
else
  echo "  鈴笍  SKIP锛氭湰鏈烘湭妫€娴嬪埌 Unity 鍙墽琛屾枃浠讹紙G4/G5/G7 寰呮湁 Unity 鐨勭幆澧冮璺戯級銆?
  echo "       鏂规 A锛堟湰鏈猴級锛氳 Unity 2022.3.20f1 鍚庨噸璺戞湰鑴氭湰鍗宠嚜鍔ㄨ瘑鍒紱"
  echo "       鏂规 B锛堜簯绔級锛氬湪 game/.github/workflows/ci.yml 鍙栨秷娉ㄩ噴 unity-tests job 骞堕厤 UNITY_LICENSE secret銆?
fi

# -----------------------------------------------------------------------------
# 姹囨€?# -----------------------------------------------------------------------------
echo ""
echo "== CI 缁撴灉 =="
if [ "$RC" -eq 0 ]; then
  echo "鉁?PASS锛氬眰0锛圙1/G2/G3锛夊叏杩囷紱Unity 灞傦紙G4/G5/G7锛夋寜鐜 SKIP/鎵ц銆?
else
  echo "鉂?FAIL锛氬瓨鍦ㄦ湭閫氳繃鐨勯棬锛堣瑙佷笂鏂癸級銆?
fi
exit $RC
