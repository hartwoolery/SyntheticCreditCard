using UnityEngine;
using System.Collections.Generic;

public class NameGenerator : MonoBehaviour
{
    [Header("Name Components")]
    public string[] firstNames = {
        "JAMES", "MARY", "JOHN", "PATRICIA", "ROBERT", "JENNIFER", "MICHAEL", "LINDA", "WILLIAM", "ELIZABETH",
        "DAVID", "BARBARA", "RICHARD", "SUSAN", "JOSEPH", "JESSICA", "THOMAS", "SARAH", "CHRISTOPHER", "KAREN",
        "CHARLES", "NANCY", "DANIEL", "LISA", "MATTHEW", "BETTY", "ANTHONY", "HELEN", "MARK", "SANDRA",
        "DONALD", "DONNA", "STEVEN", "CAROL", "PAUL", "RUTH", "ANDREW", "SHARON", "JOSHUA", "MICHELLE",
        "KENNETH", "LAURA", "KEVIN", "EMILY", "BRIAN", "KIMBERLY", "GEORGE", "DEBORAH", "EDWARD", "DOROTHY",
        "RONALD", "LISA", "TIMOTHY", "NANCY", "JASON", "KAREN", "JEFFREY", "BETTY", "RYAN", "HELEN",
        "JACOB", "SANDRA", "GARY", "DONNA", "NICHOLAS", "CAROL", "ERIC", "RUTH", "JONATHAN", "SHARON",
        "STEPHEN", "MICHELLE", "LARRY", "LAURA", "JUSTIN", "EMILY", "SCOTT", "KIMBERLY", "BRANDON", "DEBORAH",
        "BENJAMIN", "DOROTHY", "SAMUEL", "LISA", "FRANK", "NANCY", "GREGORY", "KAREN", "RAYMOND", "BETTY",
        "ALEXANDER", "HELEN", "PATRICK", "SANDRA", "JACK", "DONNA", "DENNIS", "CAROL", "JERRY", "RUTH"
    };
    
    public string[] lastNames = {
        "SMITH", "JOHNSON", "WILLIAMS", "BROWN", "JONES", "GARCIA", "MILLER", "DAVIS", "RODRIGUEZ", "MARTINEZ",
        "HERNANDEZ", "LOPEZ", "GONZALEZ", "WILSON", "ANDERSON", "THOMAS", "TAYLOR", "MOORE", "JACKSON", "MARTIN",
        "LEE", "PEREZ", "THOMPSON", "WHITE", "HARRIS", "SANCHEZ", "CLARK", "RAMIREZ", "LEWIS", "ROBINSON",
        "WALKER", "YOUNG", "ALLEN", "KING", "WRIGHT", "SCOTT", "TORRES", "NGUYEN", "HILL", "FLORES",
        "GREEN", "ADAMS", "NELSON", "BAKER", "HALL", "RIVERA", "CAMPBELL", "MITCHELL", "CARTER", "ROBERTS",
        "GOMEZ", "PHILLIPS", "EVANS", "TURNER", "DIAZ", "PARKER", "CRUZ", "EDWARDS", "COLLINS", "REYES",
        "STEWART", "MORRIS", "MORALES", "MURPHY", "COOK", "ROGERS", "GUTIERREZ", "ORTIZ", "MORGAN", "COOPER",
        "PETERSON", "BAILEY", "REED", "KELLY", "HOWARD", "RAMOS", "KIM", "COX", "WARD", "RICHARDSON",
        "WATSON", "BROOKS", "CHAVEZ", "WOOD", "JAMES", "BENNETT", "GRAY", "MENDOZA", "RUIZ", "HUGHES",
        "PRICE", "ALVAREZ", "CASTILLO", "SANDERS", "PATEL", "MYERS", "LONG", "ROSS", "FOSTER", "JIMENEZ"
    };
    
    public string[] middleInitials = {
        "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
        "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"
    };
    
    [Header("Name Formats")]
    public bool useMiddleInitial = true;
    public float middleInitialProbability = 0.3f;
    public bool useSuffix = false;
    public string[] suffixes = { "JR", "SR", "II", "III", "IV" };
    public float suffixProbability = 0.1f;
    
    [Header("Name Styles")]
    public NameStyle[] nameStyles = {
        NameStyle.FIRST_LAST,
        NameStyle.FIRST_MIDDLE_LAST,
        NameStyle.FIRST_LAST_SUFFIX,
        NameStyle.FIRST_MIDDLE_LAST_SUFFIX,
        NameStyle.LAST_FIRST,
        NameStyle.LAST_FIRST_MIDDLE
    };
    
    public enum NameStyle
    {
        FIRST_LAST,
        FIRST_MIDDLE_LAST,
        FIRST_LAST_SUFFIX,
        FIRST_MIDDLE_LAST_SUFFIX,
        LAST_FIRST,
        LAST_FIRST_MIDDLE
    }
    
    public string GenerateName()
    {
        string firstName = firstNames[Random.Range(0, firstNames.Length)];
        string lastName = lastNames[Random.Range(0, lastNames.Length)];
        string middleInitial = "";
        string suffix = "";
        
        // Add middle initial
        if (useMiddleInitial && Random.Range(0f, 1f) < middleInitialProbability)
        {
            middleInitial = middleInitials[Random.Range(0, middleInitials.Length)];
        }
        
        // Add suffix
        if (useSuffix && Random.Range(0f, 1f) < suffixProbability)
        {
            suffix = suffixes[Random.Range(0, suffixes.Length)];
        }
        
        // Choose random name style
        NameStyle style = nameStyles[Random.Range(0, nameStyles.Length)];
        
        return FormatName(firstName, middleInitial, lastName, suffix, style);
    }
    
    string FormatName(string firstName, string middleInitial, string lastName, string suffix, NameStyle style)
    {
        switch (style)
        {
            case NameStyle.FIRST_LAST:
                return $"{firstName} {lastName}";
                
            case NameStyle.FIRST_MIDDLE_LAST:
                if (!string.IsNullOrEmpty(middleInitial))
                {
                    return $"{firstName} {middleInitial}. {lastName}";
                }
                return $"{firstName} {lastName}";
                
            case NameStyle.FIRST_LAST_SUFFIX:
                if (!string.IsNullOrEmpty(suffix))
                {
                    return $"{firstName} {lastName} {suffix}";
                }
                return $"{firstName} {lastName}";
                
            case NameStyle.FIRST_MIDDLE_LAST_SUFFIX:
                string result = firstName;
                if (!string.IsNullOrEmpty(middleInitial))
                {
                    result += $" {middleInitial}.";
                }
                result += $" {lastName}";
                if (!string.IsNullOrEmpty(suffix))
                {
                    result += $" {suffix}";
                }
                return result;
                
            case NameStyle.LAST_FIRST:
                return $"{lastName}, {firstName}";
                
            case NameStyle.LAST_FIRST_MIDDLE:
                if (!string.IsNullOrEmpty(middleInitial))
                {
                    return $"{lastName}, {firstName} {middleInitial}.";
                }
                return $"{lastName}, {firstName}";
                
            default:
                return $"{firstName} {lastName}";
        }
    }
    
    // Generate multiple names for variety
    public string[] GenerateNames(int count)
    {
        string[] names = new string[count];
        for (int i = 0; i < count; i++)
        {
            names[i] = GenerateName();
        }
        return names;
    }
    
    // Generate a name with specific style
    public string GenerateNameWithStyle(NameStyle style)
    {
        string firstName = firstNames[Random.Range(0, firstNames.Length)];
        string lastName = lastNames[Random.Range(0, lastNames.Length)];
        string middleInitial = middleInitials[Random.Range(0, middleInitials.Length)];
        string suffix = suffixes[Random.Range(0, suffixes.Length)];
        
        return FormatName(firstName, middleInitial, lastName, suffix, style);
    }
}
