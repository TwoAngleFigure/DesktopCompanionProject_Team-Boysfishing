using System;
using System.Collections.Generic;
using UnityEngine;
using DesktopCompanion.Data;

namespace DesktopCompanion.Data
{
    public enum RecipeResultType
    {
        Specific,
        RandomDrop,
        None
    }


    [Serializable]
    public struct RecipeIngredient
    {
        [Tooltip("재료의 큰 분류 (예: Materials, Consumables 등)")]
        public ItemType itemType;

        [Tooltip("필요한 아이템의 고유 Data ID (JSON에 정의된 300006 등)")]
        public int dataId;

        [Tooltip("한 번 조합할 때 소모될 아이템의 개수")]
        public int amount;
    }

    [Serializable]
    public struct RecipeResult
    {
        [Tooltip("보상 지급 방식 (확정 지급인지, 랜덤 테이블을 굴릴 것인지)")]
        public RecipeResultType resultType;

        [Tooltip("결과물의 큰 분류 (예: Equipment, Fish 등)")]
        public ItemType itemType;

        [Tooltip("지급할 아이템의 고유 ID (랜덤일 경우 DropTable ID를 넣는 용도로 재사용 가능)")]
        public int dataId;

        [Tooltip("지급할 결과물의 개수")]
        public int amount;
    }

    [CreateAssetMenu(
        fileName = "New Mixture Recipe",
        menuName = "DesktopCompanion/Mixture Recipe"
        )]
    public class MixtureRecipeSO : ScriptableObject
    {
        [Header("조합 기본 정보")]
        [Tooltip("UI에 표시되거나 기획자가 식별하기 위한 조합테이블의 이름")]
        public string recipeName;

        [Header("필요 재료 목록")]
        [Tooltip("List(가변 배열)를 사용하여 재료의 종류를 1개부터 무한대까지 자유롭게 추가할 수 있게 만듭니다.")]
        public List<RecipeIngredient> ingredients = new List<RecipeIngredient>();

        [Header("조합 결과물")]
        [Tooltip("조합 성공 시 유저가 받게 될 최종 결과물 설정")]
        public RecipeResult result;
    }
}