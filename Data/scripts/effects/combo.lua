function onGain(owner, effect, actionContainer)
end;

function onLose(owner, effect, actionContainer, replacing)
    if replacing then return end
    owner:SetCombos();
end;