# Desktop Companion ÇÁ·¹ÀÓ¿öÅ© ·ê (ÆÀ¿ø °¡ÀÌµå)

ÇÁ·ÎÁ§Æ®¸¦ ÁøÇàÇÏ¸ç ÄÚµå ÀÛ¼º/¼öÁ¤ ½Ã ´ÙÀ½ÀÇ 3°¡Áö ÇÁ·¹ÀÓ¿öÅ© ¾ÆÅ°ÅØÃ³ ¿øÄ¢À» ¹«Á¶°Ç ÁØ¼öÇÕ´Ï´Ù.

## 1. WorldManager API (3D ¿ùµå ºä °ü¸®)
- **WorldViewBase »ó¼Ó**: ¿ùµå ¿ÀºêÁ§Æ®´Â WorldViewBase¸¦ »ó¼Ó. WorldManager°¡ °ü¸®.
- **Bind() / Unbind()**: ÀÚ½ÅÀÇ System Action ÀÌº¥Æ®¸¦ ±¸µ¶(+=)/ÇØÁ¦(-=)¸¸ ÀÛ¼º. SystemManager.GetSystem<T>() È°¿ë.
- **´Ü¹æÇâ µ¥ÀÌÅÍ Èå¸§**: System(»óÅÂ º¯È­) ¡æ Action ¹ßÇà ¡æ À¯´ÖÀÌ ±¸µ¶ÇÏ¿© ¿ùµå ¿ÀºêÁ§Æ® °»½Å(AssetProvider È°¿ë).
- **ÆäÀÌ·Îµå ±ÔÄ¢**: SystemÀº Action ÀÌº¥Æ®¿¡ Entity °´Ã¼ ÂüÁ¶¸¦ ³Ñ±â¸é ¾È µÊ! ¿ÀÁ÷ "½Äº°ÀÚ(EntityHandle) + ¿ø½Ã/ºÒº¯ °ª(int, Vector3 µî)"¸¸ Àü´ŞÇÒ °Í.

## 2. UIManager API (UI Áß¾Ó °ü¸® ¹× MVVM ÅëÀÏ)
- **UI Base Å¬·¡½º »ó¼Ó**: 
  - ÀÏ¹İ UI´Â UIViewBase »ó¼Ó.
  - Ã¢ UI(ÀÎº¥Åä¸® µî)´Â UIWindowBase »ó¼Ó (½ºÅÃ ¹× ¿ìÅ¬¸¯ ´İ±â ÀÚµ¿ Âü¿©).
- **ViewModel ±¸Çö (UIViewModelBase)**:
  - Bind() / Unbind()¿¡¼­ SystemÀÇ ActionÀ» ±¸µ¶/ÇØÁ¦.
  - ºä °»½ÅÀ» À§ÇØ BindableProperty<T> »ç¿ë (°ª º¯°æ ÅëÁö).
  - ºä ÀÔ·ÂÀ» Ã³¸®ÇÏ±â À§ÇØ RelayCommand Á¤ÀÇ.
- **¾ç¹æÇâ Åë½Å (MVVM)**: 
  - [View] ¡æ RelayCommand ¡æ [ViewModel] ¡æ System ¸Ş¼­µå È£Ãâ
  - [System] ¡æ Action ÀÌº¥Æ® ¡æ [ViewModel] ¡æ BindableProperty ¡æ [View]

## 3. ÇÁ·¹ÀÓ¿öÅ© ÄÚ¾î ¹× ¾ÆÅ°ÅØÃ³ ±ÔÄ¢
- **ÀÇÁ¸¼º ¹æÇâ¼º**: SystemÀº EntityManager¿Í ´Ù¸¥ System(ÅëÇØ SystemManager)À» ÂüÁ¶ °¡´ÉÇÏÁö¸¸, EntityManager´Â µµ¸ŞÀÎÀ» ¾Ë¸é ¾È µÇ¸ç, SystemÀº UI³ª View¸¦ Á÷Á¢ ÂüÁ¶ÇÏ¸é ¾È µÊ.
- **Entity**: ·ÎÁ÷ ºÒ°¡. »óÅÂ(Á¤Àû Data´Â DataManager¿¡¼­ ÀĞ°í, ·±Å¸ÀÓ °¡º¯ »óÅÂ´Â Entity¿¡ ÀúÀå)¿Í EntityHandle¸¸ À¯Áö. 
- **System**:
  - SystemBase »ó¼Ó. µµ¸ŞÀÎ ±â´É ¹× °¡º¯ »óÅÂ ¼ÒÀ¯.
  - EntityÀÇ »ı¼º, Á¶È¸, ¼Ò¸êÀº ¹«Á¶°Ç EntityManager¸¦ ÅëÇØ¼­ ¼öÇà (Create, Get, Destroy).
  - Entity ÀÚÃ¼¸¦ º¯¼ö¿¡ ´ãÁö ¸»°í EntityHandle·Î¸¸ º¸°üÇÒ °Í.
  - Update°¡ ÇÊ¿äÇÏ´Ù¸é ITickableÀ» ±¸ÇöÇÏ¿© Tick(float deltaTime)¿¡¼­ Ã³¸® (MonoBehaviour ÄÚ·çÆ¾ ºÒ°¡).
- **Data (SO)**: ¼öÁ¤ ºÒ°¡´ÉÇÑ Á¤ÀÇ/Ã»»çÁø. Ãß°¡ ½Ã GameManager.RegisterSystems/RegisterEntityFactories¿¡ µî·Ï ¹× ¼¼ÆÃ.
- **¼¼ÀÌºê (ISaveable)**: ¿µ±¸ »óÅÂ ÀúÀåÀº DTO (POCO)¿¡ Data ID¿Í °¡º¯ »óÅÂ¸¸ Æ÷ÇÔ. Entity ÂüÁ¶´Â ¹®ÀÚ¿­ º¯È¯ (ToString("N"))ÇÏ¿© ÀúÀå.
- **ºñÁÖ¾ó ¿¡¼Â**: Data¿¡¼­ ¿¡¼Â Á÷Á¢ ÂüÁ¶ ±İÁö. Addressables Å° ±Ô¾à({Å¬·¡½º¸í}_{ID}_{¿ëµµ})¸¦ µû¸£°í, View ´Ü¿¡¼­¸¸ AssetProvider·Î Á¶È¸ÇÏ¿© ¼¼ÆÃ (AssetKeys.Of(data, usage)).

## 4. ì‘ì—… ê¶Œí•œ ë° í´ë” ì ‘ê·¼ ê·œì¹™
- **1_KSS í´ë” ì „ìš©**: ì½”ë“œ ìˆ˜ì • ë° ìƒì„±ì€ ë°˜ë“œì‹œ 1_KSS í´ë” ë‚´ì—ì„œë§Œ ì´ë£¨ì–´ì ¸ì•¼ í•©ë‹ˆë‹¤.
- **íƒ€ í´ë” ì½ê¸° ì „ìš©**: 1_KSS ì´ì™¸ì˜ ë‹¤ë¥¸ í´ë”ì— ìˆëŠ” íŒŒì¼ë“¤ì€ ì½”ë“œ íë¦„ íŒŒì•…ì„ ìœ„í•´ ì½ê¸°ë§Œ ê°€ëŠ¥í•˜ë©°, ì ˆëŒ€ ìˆ˜ì •í•˜ê±°ë‚˜ ìƒˆë¡œìš´ íŒŒì¼ì„ ìƒì„±í•´ì„œëŠ” ì•ˆ ë©ë‹ˆë‹¤. (ë‹¤ë¥¸ íŒ€ì›ë“¤ì˜ ì½”ë“œë¥¼ ë³´í˜¸í•˜ê¸° ìœ„í•¨ì…ë‹ˆë‹¤.)
