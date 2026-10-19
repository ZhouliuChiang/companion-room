# 粉金塔羅牌美術

完整 78 張：22 張大阿爾克那，56 張小阿爾克那。卡面檔名見 Resources/CompanionRoom/Tarot/art_manifest.json。

風格：粉金星月裝飾框、上方編號圓章、下方奶油色英文名牌；中央採簡單平面 Q 版圖示，保留牌的主要辨識元素。卡背沿用粉色月兔設計。

卡面：Assets/CompanionRoom/Resources/CompanionRoom/Tarot/Cards/<id>.png
卡背：Assets/CompanionRoom/Resources/CompanionRoom/Tarot/Art/card_back.png
模型：Assets/CompanionRoom/Art/PastelTarot/Prefabs/<id>.prefab
獨立 FBX 模型：Assets/CompanionRoom/Art/PastelTarot/Models/<id>.fbx
可編輯 Blender 原檔：Concepts/PastelTarot78.blend

模型尺寸 70 × 115 mm，厚度 1.6 mm，圓角 4 mm。中心旋轉軸；正面朝本地 +Z；正面、背面、側邊各一個材質槽，背面 UV 可正確閱讀。可直接對整張卡的 Transform 做抽牌移動或 Y 軸翻牌；美術貼圖沒有烘焙透視與厚度。

在 Unity 執行「Tools/兔兔房間/建立粉色塔羅牌模型」可重建模型與總覽圖。占卜畫面使用 TarotSolidGraphic 的圓角厚卡網格翻轉，厚度與 FBX 同為 1.6/70 寬度比例；牌轉至 90 度仍有側邊。桌上的塔羅物件會以 TarotDeckVisual 綁定新月兔牌堆。既有 RPG 問答、洗牌發牌、正逆位與手帳紀錄流程沿用原有介面。
牌組已擴充至 78 張並補齊小牌正逆位解讀；原有 22 張大牌解讀保留。圖檔 ID、牌義 ID 與英文名均對應，逆位機率維持原設定。
FBX 及 Blender 原檔由 Tools/build_pastel_tarot.py 匯出；每個 FBX 在原點匯出，Blender 原檔則以總覽方式排好。FBX 的卡面、卡背與側邊有獨立材質。Unity Prefab 需執行上述選單建立。

## 生成方式與提示詞

使用內建 image_gen（未使用 API/CLI）。定稿以太陽卡為共同版型參考，每張獨立生成。

共同提示詞：

Use case: illustration-story. Production Unity tarot FACE texture, SINGLE portrait card aspect 214:350 full image. DESIGN: ornamental frame exactly inspired by supplied reference: blush pink rounded outer edge, thin elegant gold inner arch, small gold corner stars and tiny leaf flourishes, small crescent and daisy at bottom corners. Round ivory numeral medallion centered TOP. Beautiful ivory outlined horizontal name plaque centered BOTTOM with elegant warm brown serif English typography and small diamond decorations. CENTER: plain warm ivory background, SIMPLE FLAT 2D KAWAII ICON illustration, very few big clean shapes, light pastel colors and minimal soft shading, large empty breathing room, no scenery or intricate detail. Keep ornate detail strictly on BORDER; central drawing is as simple as a sun or crescent icon. NO realistic characters, NO sculptural textured characters, NO embroidered clothing, NO dense background. Card is flat orthographic, no perspective, no thickness drawn in image, no watermark. 

每張追加正確編號、英文名及下列中央圖案；保持參考邊框與字體，人物採簡單平面 Q 版。小牌數字牌要求正確且可逐一數出的花色數量。

- fool | 0 | THE FOOL | tiny cute traveler with stick bundle and small white dog at a simple cliff edge
- magician | I | THE MAGICIAN | cute simple magician holding raised wand, small table with cup sword pentacle and wand
- high_priestess | II | THE HIGH PRIESTESS | cute simple priestess with moon crown between two short pillars, holding scroll
- empress | III | THE EMPRESS | cute crowned queen holding scepter with wheat sprigs and heart Venus shield
- emperor | IV | THE EMPEROR | cute crowned bearded king seated on simple ram throne holding orb
- hierophant | V | THE HIEROPHANT | cute priest with triple crown and staff, two crossed golden keys below
- lovers | VI | THE LOVERS | two cute chibi people holding hands below one big pink heart and small angel wings
- chariot | VII | THE CHARIOT | cute crowned rider in a small chariot with black and white sphinx pair
- strength | VIII | STRENGTH | cute girl gently petting a tiny friendly lion, infinity symbol above
- hermit | IX | THE HERMIT | cute hooded elderly hermit holding big glowing lantern and walking staff
- wheel_of_fortune | X | WHEEL OF FORTUNE | one large golden eight-spoke wheel with tiny sphinx on top and two curved arrows
- justice | XI | JUSTICE | cute crowned judge holding balanced golden scales and upright sword
- hanged_man | XII | THE HANGED MAN | cute peaceful person upside-down hanging by one ankle from simple T tree, bent free leg and golden halo
- death | XIII | DEATH | cute tiny skeleton knight on white pony holding black flag with white flower, no horror
- temperance | XIV | TEMPERANCE | cute simple winged angel pouring water between TWO large cups
- devil | XV | THE DEVIL | cute lavender horned winged imp with two loose chain loops below, tiny inverted star
- tower | XVI | THE TOWER | one simple tower struck by big yellow lightning, crown roof tipped off, two small flame shapes
- star | XVII | THE STAR | ONE very large soft pink five point star centered, two small four-point pink sparkles and few golden dots, as minimal as reference
- moon | XVIII | THE MOON | ONE very large lavender crescent centered, three small lavender four-point sparkles and few golden dots, as minimal as reference
- sun | XIX | THE SUN | ONE very large butter yellow sun disk with ten simple rounded triangular rays, two tiny golden sparkles and few dots, as minimal as reference
- judgement | XX | JUDGEMENT | one cute chibi angel blowing a big golden trumpet with white red-cross flag, three tiny raised hands below
- world | XXI | THE WORLD | one cute simple dancing chibi person inside oval sage laurel wreath with two pink ribbon ties, holding two short wands
- ace_of_wands | 1 | ACE OF WANDS | EXACTLY 1 separate large wooden wand with two tiny green leaves icons, carefully arranged in a clean balanced pip-card layout, all 1 clearly visible and countable, do not add any other suit icons. one large central emblem emerging from a tiny cloud. Use warm peach and sage for central illustration.
- two_of_wands | 2 | TWO OF WANDS | EXACTLY 2 separate large wooden wand with two tiny green leaves icons, carefully arranged in a clean balanced pip-card layout, all 2 clearly visible and countable, do not add any other suit icons. . Use warm peach and sage for central illustration.
- three_of_wands | 3 | THREE OF WANDS | EXACTLY 3 separate large wooden wand with two tiny green leaves icons, carefully arranged in a clean balanced pip-card layout, all 3 clearly visible and countable, do not add any other suit icons. . Use warm peach and sage for central illustration.
- four_of_wands | 4 | FOUR OF WANDS | EXACTLY 4 separate large wooden wand with two tiny green leaves icons, carefully arranged in a clean balanced pip-card layout, all 4 clearly visible and countable, do not add any other suit icons. . Use warm peach and sage for central illustration.
- five_of_wands | 5 | FIVE OF WANDS | EXACTLY 5 separate large wooden wand with two tiny green leaves icons, carefully arranged in a clean balanced pip-card layout, all 5 clearly visible and countable, do not add any other suit icons. . Use warm peach and sage for central illustration.
- six_of_wands | 6 | SIX OF WANDS | EXACTLY 6 separate large wooden wand with two tiny green leaves icons, carefully arranged in a clean balanced pip-card layout, all 6 clearly visible and countable, do not add any other suit icons. . Use warm peach and sage for central illustration.
- seven_of_wands | 7 | SEVEN OF WANDS | EXACTLY 7 separate large wooden wand with two tiny green leaves icons, carefully arranged in a clean balanced pip-card layout, all 7 clearly visible and countable, do not add any other suit icons. . Use warm peach and sage for central illustration.
- eight_of_wands | 8 | EIGHT OF WANDS | EXACTLY 8 separate large wooden wand with two tiny green leaves icons, carefully arranged in a clean balanced pip-card layout, all 8 clearly visible and countable, do not add any other suit icons. . Use warm peach and sage for central illustration.
- nine_of_wands | 9 | NINE OF WANDS | EXACTLY 9 separate large wooden wand with two tiny green leaves icons, carefully arranged in a clean balanced pip-card layout, all 9 clearly visible and countable, do not add any other suit icons. . Use warm peach and sage for central illustration.
- ten_of_wands | 10 | TEN OF WANDS | EXACTLY 10 separate large wooden wand with two tiny green leaves icons, carefully arranged in a clean balanced pip-card layout, all 10 clearly visible and countable, do not add any other suit icons. . Use warm peach and sage for central illustration.
- page_of_wands | PAGE | PAGE OF WANDS | one tiny flat chibi PAGE, young apprentice standing holding ONE wooden wand with two tiny green leaves, simple short tunic and small feather cap. Use warm peach and sage for central illustration.
- knight_of_wands | KNIGHT | KNIGHT OF WANDS | one flat chibi KNIGHT wearing simple armor riding small pony and holding ONE wooden wand with two tiny green leaves. Use warm peach and sage for central illustration.
- queen_of_wands | QUEEN | QUEEN OF WANDS | one flat chibi QUEEN with crown seated on simple throne holding ONE wooden wand with two tiny green leaves, feminine long hair. Use warm peach and sage for central illustration.
- king_of_wands | KING | KING OF WANDS | one flat chibi KING with crown and short beard seated on simple throne holding ONE wooden wand with two tiny green leaves. Use warm peach and sage for central illustration.
- ace_of_cups | 1 | ACE OF CUPS | EXACTLY 1 separate large golden chalice cup icons, carefully arranged in a clean balanced pip-card layout, all 1 clearly visible and countable, do not add any other suit icons. one large central emblem emerging from a tiny cloud. Use pastel lavender and blush for central illustration.
- two_of_cups | 2 | TWO OF CUPS | EXACTLY 2 separate large golden chalice cup icons, carefully arranged in a clean balanced pip-card layout, all 2 clearly visible and countable, do not add any other suit icons. . Use pastel lavender and blush for central illustration.
- three_of_cups | 3 | THREE OF CUPS | EXACTLY 3 separate large golden chalice cup icons, carefully arranged in a clean balanced pip-card layout, all 3 clearly visible and countable, do not add any other suit icons. . Use pastel lavender and blush for central illustration.
- four_of_cups | 4 | FOUR OF CUPS | EXACTLY 4 separate large golden chalice cup icons, carefully arranged in a clean balanced pip-card layout, all 4 clearly visible and countable, do not add any other suit icons. . Use pastel lavender and blush for central illustration.
- five_of_cups | 5 | FIVE OF CUPS | EXACTLY 5 separate large golden chalice cup icons, carefully arranged in a clean balanced pip-card layout, all 5 clearly visible and countable, do not add any other suit icons. . Use pastel lavender and blush for central illustration.
- six_of_cups | 6 | SIX OF CUPS | EXACTLY 6 separate large golden chalice cup icons, carefully arranged in a clean balanced pip-card layout, all 6 clearly visible and countable, do not add any other suit icons. . Use pastel lavender and blush for central illustration.
- seven_of_cups | 7 | SEVEN OF CUPS | EXACTLY 7 separate large golden chalice cup icons, carefully arranged in a clean balanced pip-card layout, all 7 clearly visible and countable, do not add any other suit icons. . Use pastel lavender and blush for central illustration.
- eight_of_cups | 8 | EIGHT OF CUPS | EXACTLY 8 separate large golden chalice cup icons, carefully arranged in a clean balanced pip-card layout, all 8 clearly visible and countable, do not add any other suit icons. . Use pastel lavender and blush for central illustration.
- nine_of_cups | 9 | NINE OF CUPS | EXACTLY 9 separate large golden chalice cup icons, carefully arranged in a clean balanced pip-card layout, all 9 clearly visible and countable, do not add any other suit icons. . Use pastel lavender and blush for central illustration.
- ten_of_cups | 10 | TEN OF CUPS | EXACTLY 10 separate large golden chalice cup icons, carefully arranged in a clean balanced pip-card layout, all 10 clearly visible and countable, do not add any other suit icons. . Use pastel lavender and blush for central illustration.
- page_of_cups | PAGE | PAGE OF CUPS | one tiny flat chibi PAGE, young apprentice standing holding ONE golden chalice cup, simple short tunic and small feather cap. Use pastel lavender and blush for central illustration.
- knight_of_cups | KNIGHT | KNIGHT OF CUPS | one flat chibi KNIGHT wearing simple armor riding small pony and holding ONE golden chalice cup. Use pastel lavender and blush for central illustration.
- queen_of_cups | QUEEN | QUEEN OF CUPS | one flat chibi QUEEN with crown seated on simple throne holding ONE golden chalice cup, feminine long hair. Use pastel lavender and blush for central illustration.
- king_of_cups | KING | KING OF CUPS | one flat chibi KING with crown and short beard seated on simple throne holding ONE golden chalice cup. Use pastel lavender and blush for central illustration.
- ace_of_swords | 1 | ACE OF SWORDS | EXACTLY 1 separate large silver straight sword with golden hilt icons, carefully arranged in a clean balanced pip-card layout, all 1 clearly visible and countable, do not add any other suit icons. one large central emblem emerging from a tiny cloud. Use pastel powder blue and lavender for central illustration.
- two_of_swords | 2 | TWO OF SWORDS | EXACTLY 2 separate large silver straight sword with golden hilt icons, carefully arranged in a clean balanced pip-card layout, all 2 clearly visible and countable, do not add any other suit icons. . Use pastel powder blue and lavender for central illustration.
- three_of_swords | 3 | THREE OF SWORDS | EXACTLY 3 separate large silver straight sword with golden hilt icons, carefully arranged in a clean balanced pip-card layout, all 3 clearly visible and countable, do not add any other suit icons. . Use pastel powder blue and lavender for central illustration.
- four_of_swords | 4 | FOUR OF SWORDS | EXACTLY 4 separate large silver straight sword with golden hilt icons, carefully arranged in a clean balanced pip-card layout, all 4 clearly visible and countable, do not add any other suit icons. . Use pastel powder blue and lavender for central illustration.
- five_of_swords | 5 | FIVE OF SWORDS | EXACTLY 5 separate large silver straight sword with golden hilt icons, carefully arranged in a clean balanced pip-card layout, all 5 clearly visible and countable, do not add any other suit icons. . Use pastel powder blue and lavender for central illustration.
- six_of_swords | 6 | SIX OF SWORDS | EXACTLY 6 separate large silver straight sword with golden hilt icons, carefully arranged in a clean balanced pip-card layout, all 6 clearly visible and countable, do not add any other suit icons. . Use pastel powder blue and lavender for central illustration.
- seven_of_swords | 7 | SEVEN OF SWORDS | EXACTLY 7 separate large silver straight sword with golden hilt icons, carefully arranged in a clean balanced pip-card layout, all 7 clearly visible and countable, do not add any other suit icons. . Use pastel powder blue and lavender for central illustration.
- eight_of_swords | 8 | EIGHT OF SWORDS | EXACTLY 8 separate large silver straight sword with golden hilt icons, carefully arranged in a clean balanced pip-card layout, all 8 clearly visible and countable, do not add any other suit icons. . Use pastel powder blue and lavender for central illustration.
- nine_of_swords | 9 | NINE OF SWORDS | EXACTLY 9 separate large silver straight sword with golden hilt icons, carefully arranged in a clean balanced pip-card layout, all 9 clearly visible and countable, do not add any other suit icons. . Use pastel powder blue and lavender for central illustration.
- ten_of_swords | 10 | TEN OF SWORDS | EXACTLY 10 separate large silver straight sword with golden hilt icons, carefully arranged in a clean balanced pip-card layout, all 10 clearly visible and countable, do not add any other suit icons. . Use pastel powder blue and lavender for central illustration.
- page_of_swords | PAGE | PAGE OF SWORDS | one tiny flat chibi PAGE, young apprentice standing holding ONE silver straight sword with golden hilt, simple short tunic and small feather cap. Use pastel powder blue and lavender for central illustration.
- knight_of_swords | KNIGHT | KNIGHT OF SWORDS | one flat chibi KNIGHT wearing simple armor riding small pony and holding ONE silver straight sword with golden hilt. Use pastel powder blue and lavender for central illustration.
- queen_of_swords | QUEEN | QUEEN OF SWORDS | one flat chibi QUEEN with crown seated on simple throne holding ONE silver straight sword with golden hilt, feminine long hair. Use pastel powder blue and lavender for central illustration.
- king_of_swords | KING | KING OF SWORDS | one flat chibi KING with crown and short beard seated on simple throne holding ONE silver straight sword with golden hilt. Use pastel powder blue and lavender for central illustration.
- ace_of_pentacles | 1 | ACE OF PENTACLES | EXACTLY 1 separate large golden coin containing clearly visible five-point pentagram icons, carefully arranged in a clean balanced pip-card layout, all 1 clearly visible and countable, do not add any other suit icons. one large central emblem emerging from a tiny cloud. Use butter gold and sage for central illustration.
- two_of_pentacles | 2 | TWO OF PENTACLES | EXACTLY 2 separate large golden coin containing clearly visible five-point pentagram icons, carefully arranged in a clean balanced pip-card layout, all 2 clearly visible and countable, do not add any other suit icons. . Use butter gold and sage for central illustration.
- three_of_pentacles | 3 | THREE OF PENTACLES | EXACTLY 3 separate large golden coin containing clearly visible five-point pentagram icons, carefully arranged in a clean balanced pip-card layout, all 3 clearly visible and countable, do not add any other suit icons. . Use butter gold and sage for central illustration.
- four_of_pentacles | 4 | FOUR OF PENTACLES | EXACTLY 4 separate large golden coin containing clearly visible five-point pentagram icons, carefully arranged in a clean balanced pip-card layout, all 4 clearly visible and countable, do not add any other suit icons. . Use butter gold and sage for central illustration.
- five_of_pentacles | 5 | FIVE OF PENTACLES | EXACTLY 5 separate large golden coin containing clearly visible five-point pentagram icons, carefully arranged in a clean balanced pip-card layout, all 5 clearly visible and countable, do not add any other suit icons. . Use butter gold and sage for central illustration.
- six_of_pentacles | 6 | SIX OF PENTACLES | EXACTLY 6 separate large golden coin containing clearly visible five-point pentagram icons, carefully arranged in a clean balanced pip-card layout, all 6 clearly visible and countable, do not add any other suit icons. . Use butter gold and sage for central illustration.
- seven_of_pentacles | 7 | SEVEN OF PENTACLES | EXACTLY 7 separate large golden coin containing clearly visible five-point pentagram icons, carefully arranged in a clean balanced pip-card layout, all 7 clearly visible and countable, do not add any other suit icons. . Use butter gold and sage for central illustration.
- eight_of_pentacles | 8 | EIGHT OF PENTACLES | EXACTLY 8 separate large golden coin containing clearly visible five-point pentagram icons, carefully arranged in a clean balanced pip-card layout, all 8 clearly visible and countable, do not add any other suit icons. . Use butter gold and sage for central illustration.
- nine_of_pentacles | 9 | NINE OF PENTACLES | EXACTLY 9 separate large golden coin containing clearly visible five-point pentagram icons, carefully arranged in a clean balanced pip-card layout, all 9 clearly visible and countable, do not add any other suit icons. . Use butter gold and sage for central illustration.
- ten_of_pentacles | 10 | TEN OF PENTACLES | EXACTLY 10 separate large golden coin containing clearly visible five-point pentagram icons, carefully arranged in a clean balanced pip-card layout, all 10 clearly visible and countable, do not add any other suit icons. . Use butter gold and sage for central illustration.
- page_of_pentacles | PAGE | PAGE OF PENTACLES | one tiny flat chibi PAGE, young apprentice standing holding ONE golden coin containing clearly visible five-point pentagram, simple short tunic and small feather cap. Use butter gold and sage for central illustration.
- knight_of_pentacles | KNIGHT | KNIGHT OF PENTACLES | one flat chibi KNIGHT wearing simple armor riding small pony and holding ONE golden coin containing clearly visible five-point pentagram. Use butter gold and sage for central illustration.
- queen_of_pentacles | QUEEN | QUEEN OF PENTACLES | one flat chibi QUEEN with crown seated on simple throne holding ONE golden coin containing clearly visible five-point pentagram, feminine long hair. Use butter gold and sage for central illustration.
- king_of_pentacles | KING | KING OF PENTACLES | one flat chibi KING with crown and short beard seated on simple throne holding ONE golden coin containing clearly visible five-point pentagram. Use butter gold and sage for central illustration.

